using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PickArr.Core.Abstractions;
using PickArr.Core.Arr;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Infrastructure.Persistence;

namespace PickArr.Infrastructure.Arr;

public sealed class ArrLibrarySync(
    PickArrDbContext db,
    IArrClientFactory clientFactory,
    IClock clock,
    ISyncRunStore syncRuns,
    ILogger<ArrLibrarySync> logger) : IArrLibrarySync
{
    public async Task SyncAsync(ArrKind kind, CancellationToken cancellationToken = default)
    {
        var client = await clientFactory.TryCreateAsync(kind, cancellationToken);
        if (client is null)
        {
            logger.LogDebug("Arr sync skipped: {Kind} not configured", kind);
            return;
        }

        var run = await syncRuns.StartAsync(
            SyncRunKinds.ArrSync,
            catalogSourceId: null,
            triggeredBy: SyncRunTriggers.Schedule,
            cancellationToken: cancellationToken);

        try
        {
            var library = await client.GetLibraryAsync(cancellationToken);
            var queue = await client.GetQueueAsync(cancellationToken);
            var queueById = queue.GroupBy(q => q.ArrItemId).ToDictionary(g => g.Key, g => g.ToList());
            var titles = await db.Titles.AsNoTracking().ToListAsync(cancellationToken);
            var kindKey = kind.ToStorage();
            var now = clock.UtcNow;
            var seenArrIds = new HashSet<int>();

            foreach (var item in library)
            {
                seenArrIds.Add(item.ArrId);
                var itemQueue = queueById.GetValueOrDefault(item.ArrId) ?? [];
                var inQueue = itemQueue.Count > 0;
                var progress = ArrStatusCalculator.QueueProgress(itemQueue);

                ArrState state;
                List<(ArrSeasonSnapshot Season, ArrState State)> seasonStates = [];
                if (kind == ArrKind.Radarr)
                {
                    state = ArrStatusCalculator.ForMovie(item.HasFile == true, inQueue);
                }
                else
                {
                    seasonStates = item.Seasons
                        .Select(s => (s, ArrStatusCalculator.ForSeason(s, inQueue && s.Monitored)))
                        .ToList();
                    state = ArrStatusCalculator.ForSeries(seasonStates, inQueue);
                }

                var entity = await db.ArrLibraryItems
                    .Include(x => x.Seasons)
                    .FirstOrDefaultAsync(x => x.Kind == kindKey && x.ArrId == item.ArrId, cancellationToken);

                if (entity is null)
                {
                    entity = new ArrLibraryItem
                    {
                        Kind = kindKey,
                        ArrId = item.ArrId,
                        ArrTitle = item.Title,
                        State = state.ToString(),
                    };
                    db.ArrLibraryItems.Add(entity);
                }

                entity.TmdbId = item.TmdbId;
                entity.TvdbId = item.TvdbId;
                entity.ImdbId = item.ImdbId;
                entity.ArrTitle = item.Title;
                entity.Year = item.Year;
                entity.Monitored = item.Monitored;
                entity.State = state.ToString();
                entity.QueueProgress = progress;
                entity.SizeOnDisk = item.SizeOnDisk;
                entity.Path = item.Path;
                entity.EpisodeFileCount = item.EpisodeFileCount;
                entity.EpisodeCount = item.EpisodeCount;
                entity.TotalEpisodeCount = item.TotalEpisodeCount;
                entity.HasFile = item.HasFile;
                entity.IsAvailable = item.IsAvailable;
                entity.SyncedAt = now;
                entity.TitleId = ArrMatcher.MatchTitleId(kind, item, titles);

                db.ArrSeasonStates.RemoveRange(entity.Seasons);
                entity.Seasons.Clear();
                foreach (var (season, seasonState) in seasonStates)
                {
                    entity.Seasons.Add(new ArrSeasonState
                    {
                        SeasonNumber = season.SeasonNumber,
                        Monitored = season.Monitored,
                        EpisodeFileCount = season.EpisodeFileCount,
                        EpisodeCount = season.EpisodeCount,
                        TotalEpisodeCount = season.TotalEpisodeCount,
                        SizeOnDisk = season.SizeOnDisk,
                        State = seasonState.ToString(),
                        QueueProgress = inQueue && season.Monitored ? progress : null,
                    });
                }
            }

            var stale = await db.ArrLibraryItems
                .Where(x => x.Kind == kindKey && !seenArrIds.Contains(x.ArrId))
                .ToListAsync(cancellationToken);
            db.ArrLibraryItems.RemoveRange(stale);

            await db.SaveChangesAsync(cancellationToken);
            await syncRuns.SucceedAsync(run.Id, cancellationToken);
            logger.LogInformation("Arr sync {Kind} upserted {Count} items", kind, library.Count);
        }
        catch (Exception ex)
        {
            // FR-ST-07: do not clear cache on failure.
            logger.LogError(ex, "Arr sync {Kind} failed; cache left unchanged", kind);
            await syncRuns.FailAsync(run.Id, ex.Message, cancellationToken);
            throw;
        }
    }

    public async Task UpsertItemAsync(ArrKind kind, ArrItemSnapshot item, CancellationToken cancellationToken = default)
    {
        var kindKey = kind.ToStorage();
        var now = clock.UtcNow;
        var titles = await db.Titles.AsNoTracking().ToListAsync(cancellationToken);

        ArrState state;
        List<(ArrSeasonSnapshot Season, ArrState State)> seasonStates = [];
        if (kind == ArrKind.Radarr)
        {
            state = ArrStatusCalculator.ForMovie(item.HasFile == true, inQueue: false);
        }
        else
        {
            seasonStates = item.Seasons
                .Select(s => (s, ArrStatusCalculator.ForSeason(s, inQueue: false)))
                .ToList();
            state = ArrStatusCalculator.ForSeries(seasonStates, anyInQueue: false);
        }

        var entity = await db.ArrLibraryItems
            .Include(x => x.Seasons)
            .FirstOrDefaultAsync(x => x.Kind == kindKey && x.ArrId == item.ArrId, cancellationToken);

        if (entity is null)
        {
            entity = new ArrLibraryItem
            {
                Kind = kindKey,
                ArrId = item.ArrId,
                ArrTitle = item.Title,
                State = state.ToString(),
            };
            db.ArrLibraryItems.Add(entity);
        }

        entity.TmdbId = item.TmdbId;
        entity.TvdbId = item.TvdbId;
        entity.ImdbId = item.ImdbId;
        entity.ArrTitle = item.Title;
        entity.Year = item.Year;
        entity.Monitored = item.Monitored;
        entity.State = state.ToString();
        entity.QueueProgress = null;
        entity.SizeOnDisk = item.SizeOnDisk;
        entity.Path = item.Path;
        entity.EpisodeFileCount = item.EpisodeFileCount;
        entity.EpisodeCount = item.EpisodeCount;
        entity.TotalEpisodeCount = item.TotalEpisodeCount;
        entity.HasFile = item.HasFile;
        entity.IsAvailable = item.IsAvailable;
        entity.SyncedAt = now;
        entity.TitleId = ArrMatcher.MatchTitleId(kind, item, titles);

        db.ArrSeasonStates.RemoveRange(entity.Seasons);
        entity.Seasons.Clear();
        foreach (var (season, seasonState) in seasonStates)
        {
            entity.Seasons.Add(new ArrSeasonState
            {
                SeasonNumber = season.SeasonNumber,
                Monitored = season.Monitored,
                EpisodeFileCount = season.EpisodeFileCount,
                EpisodeCount = season.EpisodeCount,
                TotalEpisodeCount = season.TotalEpisodeCount,
                SizeOnDisk = season.SizeOnDisk,
                State = seasonState.ToString(),
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveItemAsync(ArrKind kind, int arrId, CancellationToken cancellationToken = default)
    {
        var kindKey = kind.ToStorage();
        var entity = await db.ArrLibraryItems
            .FirstOrDefaultAsync(x => x.Kind == kindKey && x.ArrId == arrId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        db.ArrLibraryItems.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
