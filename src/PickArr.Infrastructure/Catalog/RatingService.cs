using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Metadata;
using PickArr.Core.Recommendations;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Persistence;

namespace PickArr.Infrastructure.Catalog;

public sealed class RatingService(
    PickArrDbContext db,
    IClock clock,
    ISettingsStore settings,
    IRecoRebuildScheduler rebuildScheduler,
    IRecoEventLogService eventLog) : IRatingService
{
    public async Task SetAsync(
        int titleId,
        string value,
        string origin,
        CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);

        if (!RatingValues.IsValid(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Rating must be Like or Dislike.");
        }

        var normalized = RatingValues.Normalize(value);
        var exists = await db.Titles.AnyAsync(t => t.Id == titleId, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException($"Title {titleId} not found.");
        }

        var now = clock.UtcNow;
        var rating = await db.Ratings.FindAsync([titleId], cancellationToken);
        if (rating is not null && string.Equals(rating.Value, normalized, StringComparison.Ordinal))
        {
            return;
        }

        if (rating is null)
        {
            db.Ratings.Add(new Rating
            {
                TitleId = titleId,
                Value = normalized,
                RatedAt = now,
            });
        }
        else
        {
            rating.Value = normalized;
            rating.RatedAt = now;
        }

        db.RatingEvents.Add(new RatingEvent
        {
            TitleId = titleId,
            Value = normalized,
            At = now,
            Origin = origin,
        });
        await db.SaveChangesAsync(cancellationToken);
        await eventLog.LogAsync(
            titleId,
            normalized == RatingValues.Dislike ? RecoEventKinds.Disliked : RecoEventKinds.Liked,
            cancellationToken: cancellationToken);
        rebuildScheduler.ScheduleDebouncedRebuild();
    }

    public async Task ClearAsync(int titleId, string origin, CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);

        var rating = await db.Ratings.FindAsync([titleId], cancellationToken);
        if (rating is null)
        {
            return;
        }

        db.Ratings.Remove(rating);
        db.RatingEvents.Add(new RatingEvent
        {
            TitleId = titleId,
            Value = null,
            At = clock.UtcNow,
            Origin = origin,
        });
        await db.SaveChangesAsync(cancellationToken);
        rebuildScheduler.ScheduleDebouncedRebuild();
    }

    public async Task<IReadOnlyList<RatingExportItemDto>> ExportAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);

        var rows = await db.Ratings.AsNoTracking()
            .Include(r => r.Title)!.ThenInclude(t => t!.Localizations)
            .Where(r => r.Title != null && r.Title.TmdbId != null && r.Title.TmdbType != null)
            .OrderByDescending(r => r.RatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(r =>
        {
            var title = r.Title!;
            var name = title.Localizations
                            .OrderByDescending(l => l.Language == LanguageFallback.English)
                            .Select(l => l.Name)
                            .FirstOrDefault()
                        ?? title.OriginalTitle
                        ?? $"Title {title.Id}";
            return new RatingExportItemDto(
                title.TmdbType!,
                title.TmdbId!.Value,
                name,
                RatingValues.ToApi(r.Value),
                r.RatedAt);
        }).ToList();
    }

    public async Task<RatingImportResultDto> ImportAsync(
        RatingImportRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);

        var mode = (request.Mode ?? "merge").Trim().ToLowerInvariant();
        if (mode is not ("merge" or "replace"))
        {
            throw new ArgumentException("mode must be merge or replace.", nameof(request));
        }

        var imported = 0;
        var unknown = 0;
        var ignored = 0;
        var now = clock.UtcNow;

        if (mode == "replace")
        {
            var existing = await db.Ratings.ToListAsync(cancellationToken);
            db.Ratings.RemoveRange(existing);
            foreach (var rating in existing)
            {
                db.RatingEvents.Add(new RatingEvent
                {
                    TitleId = rating.TitleId,
                    Value = null,
                    At = now,
                    Origin = RatingEventOrigins.Import,
                });
            }
        }

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.TmdbType) || item.TmdbId <= 0 || !RatingValues.IsValid(item.Value))
            {
                ignored++;
                continue;
            }

            var tmdbType = item.TmdbType.Trim();
            var title = await db.Titles.FirstOrDefaultAsync(
                t => t.TmdbId == item.TmdbId
                     && t.TmdbType != null
                     && t.TmdbType.ToLower() == tmdbType.ToLower(),
                cancellationToken);
            if (title is null)
            {
                unknown++;
                continue;
            }

            var normalized = RatingValues.Normalize(item.Value);
            var rating = await db.Ratings.FindAsync([title.Id], cancellationToken);
            if (rating is not null && string.Equals(rating.Value, normalized, StringComparison.Ordinal))
            {
                ignored++;
                continue;
            }

            if (rating is null)
            {
                db.Ratings.Add(new Rating
                {
                    TitleId = title.Id,
                    Value = normalized,
                    RatedAt = now,
                });
            }
            else
            {
                rating.Value = normalized;
                rating.RatedAt = now;
            }

            db.RatingEvents.Add(new RatingEvent
            {
                TitleId = title.Id,
                Value = normalized,
                At = now,
                Origin = RatingEventOrigins.Import,
            });
            imported++;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (imported > 0 || mode == "replace")
        {
            rebuildScheduler.ScheduleDebouncedRebuild();
        }

        return new RatingImportResultDto(imported, unknown, ignored);
    }

    public async Task OnboardingAsync(
        IReadOnlyList<RatingOnboardingItem> items,
        CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);
        var changed = false;
        var now = clock.UtcNow;

        foreach (var item in items)
        {
            if (item.Value is null || string.IsNullOrWhiteSpace(item.Value))
            {
                continue;
            }

            if (!RatingValues.IsValid(item.Value))
            {
                continue;
            }

            var exists = await db.Titles.AnyAsync(t => t.Id == item.TitleId, cancellationToken);
            if (!exists)
            {
                continue;
            }

            var normalized = RatingValues.Normalize(item.Value);
            var rating = await db.Ratings.FindAsync([item.TitleId], cancellationToken);
            if (rating is not null && string.Equals(rating.Value, normalized, StringComparison.Ordinal))
            {
                continue;
            }

            if (rating is null)
            {
                db.Ratings.Add(new Rating
                {
                    TitleId = item.TitleId,
                    Value = normalized,
                    RatedAt = now,
                });
            }
            else
            {
                rating.Value = normalized;
                rating.RatedAt = now;
            }

            db.RatingEvents.Add(new RatingEvent
            {
                TitleId = item.TitleId,
                Value = normalized,
                At = now,
                Origin = RatingEventOrigins.Onboarding,
            });
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        rebuildScheduler.ScheduleDebouncedRebuild();
    }

    private async Task EnsureEnabledAsync(CancellationToken cancellationToken)
    {
        var raw = await settings.GetRawAsync(SettingsKeys.RecoEnabled, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var enabled = System.Text.Json.JsonSerializer.Deserialize<bool>(raw);
        if (!enabled)
        {
            throw new RecoDisabledException();
        }
    }
}

public sealed class RecoDisabledException : InvalidOperationException
{
    public RecoDisabledException() : base("Recommendations feature is disabled.")
    {
    }
}
