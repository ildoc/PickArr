using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Catalog;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Metadata;
using PickArr.Infrastructure.Persistence;

namespace PickArr.Infrastructure.Catalog;

public sealed class CatalogUpserter(PickArrDbContext db, IClock clock) : ICatalogUpserter
{
    public async Task<UpsertPageResult> UpsertPageAsync(
        int catalogSourceId,
        IReadOnlyList<CatalogEntry> entries,
        Guid? syncRunId,
        bool baselineCompleted,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var titlesAdded = 0;
        var titlesUpdated = 0;
        var availAdded = 0;
        var availReadded = 0;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var entry in entries)
        {
            var (title, created) = await ResolveTitleAsync(entry, now, cancellationToken);
            if (created)
            {
                titlesAdded++;
            }
            else
            {
                titlesUpdated += TouchTitle(title, entry, now) ? 1 : 0;
            }

            var outcome = await UpsertAvailabilityAsync(
                title.Id,
                catalogSourceId,
                now,
                baselineCompleted,
                syncRunId,
                cancellationToken);
            if (outcome == AvailabilityUpsertOutcome.Added)
            {
                availAdded++;
            }
            else if (outcome == AvailabilityUpsertOutcome.Readded)
            {
                availReadded++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new UpsertPageResult(titlesAdded, titlesUpdated, availAdded, availReadded);
    }

    private async Task<(Title Title, bool Created)> ResolveTitleAsync(
        CatalogEntry entry,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var watchmodeId = TryWatchmodeId(entry.ExternalId);
        var (tmdbId, tmdbType) = NormalizeTmdb(entry.TmdbId, entry.TmdbType);

        // Local first: a title updated earlier in this page is not visible to SQL until SaveChanges.
        Title? title = FindTracked(watchmodeId, tmdbId, tmdbType);
        if (title is null && watchmodeId is not null)
        {
            title = await db.Titles.FirstOrDefaultAsync(t => t.WatchmodeId == watchmodeId, cancellationToken);
        }

        if (title is null && tmdbId is not null)
        {
            title = await db.Titles.FirstOrDefaultAsync(
                t => t.TmdbId == tmdbId && t.TmdbType == tmdbType,
                cancellationToken);
        }

        if (title is not null)
        {
            return (title, Created: false);
        }

        title = new Title
        {
            WatchmodeId = watchmodeId,
            TmdbId = tmdbId,
            TmdbType = tmdbType,
            ImdbId = entry.ImdbId,
            MediaType = CatalogMediaMapping.ToMediaType(entry.Type),
            Category = CatalogMediaMapping.ToCategory(entry.Type),
            OriginalTitle = entry.Title,
            OriginalTitleNormalized = TextNormalizer.NormalizeName(entry.Title),
            ReleaseYear = entry.Year,
            MetadataStatus = CatalogMediaMapping.InitialMetadataStatus(tmdbId),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Titles.Add(title);
        await db.SaveChangesAsync(cancellationToken);
        return (title, Created: true);
    }

    private static bool TouchTitle(Title title, CatalogEntry entry, DateTime now)
    {
        var changed = false;
        var watchmodeId = TryWatchmodeId(entry.ExternalId);
        if (title.WatchmodeId is null && watchmodeId is not null)
        {
            title.WatchmodeId = watchmodeId;
            changed = true;
        }

        var (tmdbId, tmdbType) = NormalizeTmdb(entry.TmdbId, entry.TmdbType);
        if (title.TmdbId is null && tmdbId is not null)
        {
            title.TmdbId = tmdbId;
            title.TmdbType = tmdbType;
            if (title.MetadataStatus == MetadataStatuses.Unmapped)
            {
                title.MetadataStatus = MetadataStatuses.Pending;
            }

            changed = true;
        }

        if (string.IsNullOrWhiteSpace(title.ImdbId) && !string.IsNullOrWhiteSpace(entry.ImdbId))
        {
            title.ImdbId = entry.ImdbId;
            changed = true;
        }

        if (!string.Equals(title.OriginalTitle, entry.Title, StringComparison.Ordinal))
        {
            title.OriginalTitle = entry.Title;
            title.OriginalTitleNormalized = TextNormalizer.NormalizeName(entry.Title);
            changed = true;
        }

        if (title.ReleaseYear != entry.Year && entry.Year is not null)
        {
            title.ReleaseYear = entry.Year;
            changed = true;
        }

        if (changed)
        {
            title.UpdatedAt = now;
        }

        return changed;
    }

    private async Task<AvailabilityUpsertOutcome> UpsertAvailabilityAsync(
        int titleId,
        int catalogSourceId,
        DateTime now,
        bool baselineCompleted,
        Guid? syncRunId,
        CancellationToken cancellationToken)
    {
        var availability = await db.Availabilities
            .FirstOrDefaultAsync(
                a => a.TitleId == titleId && a.CatalogSourceId == catalogSourceId,
                cancellationToken);

        if (availability is null)
        {
            availability = new Availability
            {
                TitleId = titleId,
                CatalogSourceId = catalogSourceId,
                FirstSeenAt = now,
                LastSeenAt = now,
                IsBaseline = !baselineCompleted,
                AddedAt = baselineCompleted ? now : null,
                MissedFullScans = 0,
            };
            db.Availabilities.Add(availability);
            await db.SaveChangesAsync(cancellationToken);

            // Added events only after baseline (FR-CAT-06 / UC-01).
            if (baselineCompleted)
            {
                db.AvailabilityEvents.Add(new AvailabilityEvent
                {
                    AvailabilityId = availability.Id,
                    Type = AvailabilityEventTypes.Added,
                    OccurredAt = now,
                    SyncRunId = syncRunId,
                });
            }

            return AvailabilityUpsertOutcome.Added;
        }

        if (availability.RemovedAt is not null)
        {
            availability.RemovedAt = null;
            availability.AddedAt = now;
            availability.IsBaseline = false;
            availability.MissedFullScans = 0;
            availability.LastSeenAt = now;
            db.AvailabilityEvents.Add(new AvailabilityEvent
            {
                AvailabilityId = availability.Id,
                Type = AvailabilityEventTypes.Readded,
                OccurredAt = now,
                SyncRunId = syncRunId,
            });
            return AvailabilityUpsertOutcome.Readded;
        }

        availability.LastSeenAt = now;
        availability.MissedFullScans = 0;
        return AvailabilityUpsertOutcome.Seen;
    }

    private Title? FindTracked(int? watchmodeId, int? tmdbId, string? tmdbType)
    {
        if (watchmodeId is not null)
        {
            var byWatchmode = db.Titles.Local.FirstOrDefault(t => t.WatchmodeId == watchmodeId);
            if (byWatchmode is not null)
            {
                return byWatchmode;
            }
        }

        if (tmdbId is null)
        {
            return null;
        }

        return db.Titles.Local.FirstOrDefault(t => t.TmdbId == tmdbId && t.TmdbType == tmdbType);
    }

    private static int? TryWatchmodeId(string externalId) =>
        int.TryParse(externalId, out var watchmodeId) ? watchmodeId : null;

    private static (int? TmdbId, string? TmdbType) NormalizeTmdb(int? tmdbId, string? tmdbType)
    {
        if (tmdbId is null or <= 0)
        {
            return (null, null);
        }

        var type = string.IsNullOrWhiteSpace(tmdbType) ? null : tmdbType.Trim();
        return (tmdbId, type);
    }

    private enum AvailabilityUpsertOutcome
    {
        Added,
        Readded,
        Seen,
    }
}
