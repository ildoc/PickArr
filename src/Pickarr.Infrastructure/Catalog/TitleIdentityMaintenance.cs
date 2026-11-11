using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Metadata;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Catalog;

/// <summary>
/// One-shot startup repairs for title identity: requeue IMDb-capable Unmapped rows,
/// consolidate duplicate IMDb rows left by pre-merge upserts, and reclassify Category
/// from persisted TMDB keyword names (no hardcoded keyword ids).
/// </summary>
public sealed class TitleIdentityMaintenance(
    PickarrDbContext db,
    ILogger<TitleIdentityMaintenance> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await RequeueUnmappedWithImdbAsync(cancellationToken);
        await ConsolidateDuplicateImdbTitlesAsync(cancellationToken);
        await ReclassifyCategoriesFromKeywordsAsync(cancellationToken);
    }

    private async Task RequeueUnmappedWithImdbAsync(CancellationToken cancellationToken)
    {
        var titles = await db.Titles
            .Where(t =>
                t.MetadataStatus == MetadataStatuses.Unmapped
                && t.ImdbId != null
                && t.ImdbId != "")
            .ToListAsync(cancellationToken);

        if (titles.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var title in titles)
        {
            title.MetadataStatus = MetadataStatuses.Pending;
            title.MetadataAttempts = 0;
            title.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Requeued {Count} Unmapped titles with IMDb ids for metadata enrichment",
            titles.Count);
    }

    private async Task ConsolidateDuplicateImdbTitlesAsync(CancellationToken cancellationToken)
    {
        var duplicateImdbIds = await db.Titles
            .Where(t => t.ImdbId != null && t.ImdbId != "")
            .GroupBy(t => t.ImdbId!)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync(cancellationToken);

        if (duplicateImdbIds.Count == 0)
        {
            return;
        }

        var merged = 0;
        foreach (var imdbId in duplicateImdbIds)
        {
            var group = await db.Titles
                .Include(t => t.Availabilities)
                .Where(t => t.ImdbId == imdbId)
                .OrderBy(t => t.Id)
                .ToListAsync(cancellationToken);

            if (group.Count < 2)
            {
                continue;
            }

            var survivor = PickSurvivor(group);
            foreach (var loser in group.Where(t => t.Id != survivor.Id).ToList())
            {
                await MergeLoserIntoSurvivorAsync(survivor, loser, cancellationToken);
                merged++;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        if (merged > 0)
        {
            logger.LogInformation("Consolidated {Count} duplicate IMDb title rows into survivors", merged);
        }
    }

    private static Title PickSurvivor(IReadOnlyList<Title> group) =>
        group
            .OrderByDescending(t => t.MetadataStatus == MetadataStatuses.Ready)
            .ThenByDescending(t => t.TmdbId is > 0)
            .ThenBy(t => t.Id)
            .First();

    private async Task MergeLoserIntoSurvivorAsync(
        Title survivor,
        Title loser,
        CancellationToken cancellationToken)
    {
        if (survivor.WatchmodeId is null && loser.WatchmodeId is not null)
        {
            survivor.WatchmodeId = loser.WatchmodeId;
        }

        if (survivor.TmdbId is null && loser.TmdbId is > 0)
        {
            survivor.TmdbId = loser.TmdbId;
            survivor.TmdbType = loser.TmdbType;
        }

        if (string.IsNullOrWhiteSpace(survivor.ImdbId) && !string.IsNullOrWhiteSpace(loser.ImdbId))
        {
            survivor.ImdbId = loser.ImdbId;
        }

        if (survivor.MetadataStatus != MetadataStatuses.Ready
            && loser.MetadataStatus == MetadataStatuses.Ready)
        {
            survivor.MetadataStatus = MetadataStatuses.Ready;
            survivor.MetadataFetchedAt = loser.MetadataFetchedAt ?? survivor.MetadataFetchedAt;
        }
        else if (survivor.MetadataStatus == MetadataStatuses.Unmapped
                 && !string.IsNullOrWhiteSpace(survivor.ImdbId))
        {
            survivor.MetadataStatus = MetadataStatuses.Pending;
        }

        foreach (var availability in loser.Availabilities.ToList())
        {
            var existing = survivor.Availabilities
                .FirstOrDefault(a => a.CatalogSourceId == availability.CatalogSourceId);
            if (existing is null)
            {
                availability.TitleId = survivor.Id;
                survivor.Availabilities.Add(availability);
                continue;
            }

            existing.LastSeenAt = Max(existing.LastSeenAt, availability.LastSeenAt);
            existing.FirstSeenAt = Min(existing.FirstSeenAt, availability.FirstSeenAt);
            if (existing.RemovedAt is not null && availability.RemovedAt is null)
            {
                existing.RemovedAt = null;
            }

            existing.MissedFullScans = Math.Min(existing.MissedFullScans, availability.MissedFullScans);
            db.Availabilities.Remove(availability);
        }

        await RemapProfileRowsAsync(survivor.Id, loser.Id, cancellationToken);
        await RemapLooseReferencesAsync(survivor.Id, loser.Id, cancellationToken);

        // Restrict FK on TitleTmdbRecommendations.RelatedTitleId — clear before delete.
        var recoLinks = await db.TitleTmdbRecommendations
            .Where(r => r.SourceTitleId == loser.Id || r.RelatedTitleId == loser.Id)
            .ToListAsync(cancellationToken);
        db.TitleTmdbRecommendations.RemoveRange(recoLinks);

        db.Titles.Remove(loser);
        survivor.UpdatedAt = DateTime.UtcNow;
    }

    private async Task RemapProfileRowsAsync(int survivorId, int loserId, CancellationToken cancellationToken)
    {
        var loserFavorites = await db.Favorites.Where(f => f.TitleId == loserId).ToListAsync(cancellationToken);
        foreach (var favorite in loserFavorites)
        {
            var exists = await db.Favorites.AnyAsync(
                f => f.ProfileId == favorite.ProfileId && f.TitleId == survivorId,
                cancellationToken);
            if (!exists)
            {
                db.Favorites.Add(new Favorite
                {
                    ProfileId = favorite.ProfileId,
                    TitleId = survivorId,
                    CreatedAt = favorite.CreatedAt,
                });
            }

            db.Favorites.Remove(favorite);
        }

        var loserRatings = await db.Ratings.Where(r => r.TitleId == loserId).ToListAsync(cancellationToken);
        foreach (var rating in loserRatings)
        {
            var exists = await db.Ratings.AnyAsync(
                r => r.ProfileId == rating.ProfileId && r.TitleId == survivorId,
                cancellationToken);
            if (!exists)
            {
                db.Ratings.Add(new Rating
                {
                    ProfileId = rating.ProfileId,
                    TitleId = survivorId,
                    Value = rating.Value,
                    RatedAt = rating.RatedAt,
                });
            }

            db.Ratings.Remove(rating);
        }

        var loserWatched = await db.Watched.Where(w => w.TitleId == loserId).ToListAsync(cancellationToken);
        foreach (var watched in loserWatched)
        {
            var exists = await db.Watched.AnyAsync(
                w => w.ProfileId == watched.ProfileId && w.TitleId == survivorId,
                cancellationToken);
            if (!exists)
            {
                db.Watched.Add(new Watched
                {
                    ProfileId = watched.ProfileId,
                    TitleId = survivorId,
                    WatchedAt = watched.WatchedAt,
                });
            }

            db.Watched.Remove(watched);
        }

        var ratingEvents = await db.RatingEvents.Where(e => e.TitleId == loserId).ToListAsync(cancellationToken);
        foreach (var evt in ratingEvents)
        {
            evt.TitleId = survivorId;
        }

        var recommendations = await db.Recommendations.Where(r => r.TitleId == loserId).ToListAsync(cancellationToken);
        db.Recommendations.RemoveRange(recommendations);

        var recoEvents = await db.RecoEventLogs.Where(e => e.TitleId == loserId).ToListAsync(cancellationToken);
        foreach (var evt in recoEvents)
        {
            evt.TitleId = survivorId;
        }
    }

    private async Task RemapLooseReferencesAsync(int survivorId, int loserId, CancellationToken cancellationToken)
    {
        var libraryItems = await db.ArrLibraryItems
            .Where(i => i.TitleId == loserId)
            .ToListAsync(cancellationToken);
        foreach (var item in libraryItems)
        {
            item.TitleId = survivorId;
        }

        var audits = await db.ArrAuditLogs
            .Where(a => a.TitleId == loserId)
            .ToListAsync(cancellationToken);
        foreach (var audit in audits)
        {
            audit.TitleId = survivorId;
        }
    }

    /// <summary>
    /// Recomputes <see cref="Title.Category"/> from stored keyword names so a bad historical
    /// keyword-id rule (e.g. matching TMDB "superhero") does not leave false StandUp labels.
    /// </summary>
    private async Task ReclassifyCategoriesFromKeywordsAsync(CancellationToken cancellationToken)
    {
        var titles = await db.Titles
            .Include(t => t.TitleKeywords)
            .ThenInclude(tk => tk.Keyword)
            .Where(t =>
                t.Category == TitleCategories.StandUp
                || t.TitleKeywords.Any())
            .ToListAsync(cancellationToken);

        if (titles.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var changed = 0;
        foreach (var title in titles)
        {
            var names = title.TitleKeywords.Select(tk => tk.Keyword?.Name);
            var desired = TmdbStandUp.ResolveCategory(title.MediaType, names);
            if (string.Equals(title.Category, desired, StringComparison.Ordinal))
            {
                continue;
            }

            title.Category = desired;
            title.UpdatedAt = now;
            changed++;
        }

        if (changed == 0)
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Reclassified Category on {Count} titles from persisted TMDB keyword names",
            changed);
    }

    private static DateTime Max(DateTime a, DateTime b) => a >= b ? a : b;

    private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;
}
