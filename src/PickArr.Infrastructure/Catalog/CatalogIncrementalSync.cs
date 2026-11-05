using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PickArr.Core.Abstractions;
using PickArr.Core.Catalog;
using PickArr.Core.Domain;
using PickArr.Infrastructure.Persistence;

namespace PickArr.Infrastructure.Catalog;

/// <summary>
/// ListPagesStrategy for scheduled/manual incremental catalog sync (spec §6.3).
/// </summary>
public sealed class CatalogIncrementalSync(
    PickArrDbContext db,
    IProviderResolver providerResolver,
    ICreditBudget creditBudget,
    ISyncRunStore syncRunStore,
    ICatalogUpserter upserter,
    IApiUsageSyncRunAccessor syncRunAccessor,
    IClock clock,
    ILogger<CatalogIncrementalSync> logger) : ICatalogIncrementalSync
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyList<CatalogEntryType> DefaultTypes =
    [
        CatalogEntryType.Movie,
        CatalogEntryType.TvSeries,
        CatalogEntryType.TvMiniseries,
        CatalogEntryType.TvSpecial,
    ];

    public async Task<CatalogSyncResult> RunAsync(
        int catalogSourceId,
        bool isManual = false,
        CancellationToken cancellationToken = default)
    {
        var source = await db.CatalogSources
            .Include(s => s.Provider)
            .FirstOrDefaultAsync(s => s.Id == catalogSourceId, cancellationToken)
            ?? throw new InvalidOperationException($"CatalogSource '{catalogSourceId}' was not found.");

        if (source.Provider is null || string.IsNullOrWhiteSpace(source.Provider.ExternalId))
        {
            throw new InvalidOperationException($"CatalogSource '{catalogSourceId}' has no provider external id.");
        }

        var trigger = isManual ? SyncRunTriggers.Manual : SyncRunTriggers.Schedule;
        var run = await syncRunStore.StartAsync(
            SyncRunKinds.CatalogIncremental,
            catalogSourceId,
            trigger,
            cancellationToken: cancellationToken);

        syncRunAccessor.CurrentSyncRunId = run.Id;
        try
        {
            var catalogProvider = providerResolver.Resolve(source.Provider.ProviderType);
            var pages = Math.Max(1, source.IncrementalPages);
            // Newest releases are fetched first. Record them as new even before the full
            // baseline, so the first import fills the New filter instead of hiding them.
            const bool recordAsNew = true;
            var titlesAdded = 0;
            var titlesUpdated = 0;

            for (var page = 1; page <= pages; page++)
            {
                const int estimatedCost = 1;
                if (!await creditBudget.TryReserveAsync(run.Id, estimatedCost, isManual, cancellationToken))
                {
                    var checkpoint = JsonSerializer.Serialize(new { page }, JsonOptions);
                    await syncRunStore.PauseAsync(run.Id, checkpoint, cancellationToken);
                    logger.LogInformation(
                        "Incremental sync paused for source {SourceId} at page {Page} (budget)",
                        catalogSourceId,
                        page);

                    var paused = await syncRunStore.GetAsync(run.Id, cancellationToken);
                    return ToResult(paused!);
                }

                CatalogPage catalogPage;
                try
                {
                    catalogPage = await catalogProvider.ListTitlesAsync(
                        new CatalogListRequest(
                            source.Provider.ExternalId,
                            source.CountryCode,
                            DefaultTypes,
                            CatalogSort.ReleaseDateDesc,
                            page,
                            catalogProvider.Capabilities.MaxPageSize),
                        cancellationToken);
                }
                catch (WatchmodeQuotaExceededException ex)
                {
                    var checkpoint = JsonSerializer.Serialize(new { page }, JsonOptions);
                    await syncRunStore.PauseAsync(run.Id, checkpoint, cancellationToken);
                    logger.LogWarning(ex, "Watchmode quota exceeded during incremental sync");
                    var paused = await syncRunStore.GetAsync(run.Id, cancellationToken);
                    return ToResult(paused!);
                }

                if (catalogPage.CreditsUsed > estimatedCost)
                {
                    var delta = catalogPage.CreditsUsed - estimatedCost;
                    var current = await syncRunStore.GetAsync(run.Id, cancellationToken);
                    if (current is not null)
                    {
                        current.CreditsUsed += delta;
                        await syncRunStore.UpdateAsync(current, cancellationToken);
                    }
                }

                var upsert = await upserter.UpsertPageAsync(
                    catalogSourceId,
                    catalogPage.Entries,
                    run.Id,
                    recordAsNew,
                    cancellationToken);

                titlesAdded += upsert.TitlesAdded;
                titlesUpdated += upsert.TitlesUpdated;

                var tracked = await syncRunStore.GetAsync(run.Id, cancellationToken);
                if (tracked is not null)
                {
                    tracked.PagesFetched = page;
                    tracked.TitlesAdded = titlesAdded;
                    tracked.TitlesUpdated = titlesUpdated;
                    await syncRunStore.UpdateAsync(tracked, cancellationToken);
                }
            }

            source.LastIncrementalAt = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await syncRunStore.SucceedAsync(run.Id, cancellationToken);
            var succeeded = await syncRunStore.GetAsync(run.Id, cancellationToken);
            return ToResult(succeeded!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Incremental sync failed for source {SourceId}", catalogSourceId);
            await syncRunStore.FailAsync(run.Id, ex.Message, cancellationToken);
            var failed = await syncRunStore.GetAsync(run.Id, cancellationToken);
            return ToResult(failed!);
        }
        finally
        {
            syncRunAccessor.CurrentSyncRunId = null;
        }
    }

    private static CatalogSyncResult ToResult(Core.Domain.Entities.SyncRun run) =>
        new(
            run.Id,
            run.Status,
            run.PagesFetched,
            run.CreditsUsed,
            run.TitlesAdded,
            run.TitlesUpdated,
            run.CheckpointJson,
            run.ErrorMessage);
}
