using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PickArr.Core.Abstractions;
using PickArr.Core.Catalog;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Persistence;

namespace PickArr.Infrastructure.Catalog;

public sealed class CatalogFullSync(
    PickArrDbContext db,
    IProviderResolver providerResolver,
    ICreditBudget creditBudget,
    ISyncRunStore syncRunStore,
    ICatalogUpserter upserter,
    ISettingsStore settingsStore,
    IApiUsageSyncRunAccessor syncRunAccessor,
    IClock clock,
    ILogger<CatalogFullSync> logger) : ICatalogFullSync
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
        string? resumeCheckpointJson = null,
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

        var trigger = resumeCheckpointJson is not null
            ? SyncRunTriggers.Resume
            : isManual ? SyncRunTriggers.Manual : SyncRunTriggers.Schedule;

        var run = await syncRunStore.StartAsync(
            SyncRunKinds.CatalogFull,
            catalogSourceId,
            trigger,
            checkpointJson: resumeCheckpointJson,
            cancellationToken: cancellationToken);

        syncRunAccessor.CurrentSyncRunId = run.Id;
        try
        {
            var catalogProvider = providerResolver.Resolve(source.Provider.ProviderType);
            var startPage = ReadCheckpointPage(resumeCheckpointJson) ?? 1;
            var baselineCompleted = source.BaselineCompletedAt is not null;
            var titlesAdded = 0;
            var titlesUpdated = 0;
            var totalPages = source.LastFullTotalPages ?? startPage;
            var runStartedAt = run.StartedAt;

            for (var page = startPage; ; page++)
            {
                const int estimatedCost = 1;
                if (!await creditBudget.TryReserveAsync(run.Id, estimatedCost, isManual, cancellationToken))
                {
                    return await PauseAsync(run.Id, page, catalogSourceId, cancellationToken);
                }

                CatalogPage catalogPage;
                try
                {
                    catalogPage = await catalogProvider.ListTitlesAsync(
                        new CatalogListRequest(
                            source.Provider.ExternalId,
                            source.CountryCode,
                            DefaultTypes,
                            CatalogSort.TitleAsc,
                            page,
                            catalogProvider.Capabilities.MaxPageSize),
                        cancellationToken);
                }
                catch (WatchmodeQuotaExceededException ex)
                {
                    logger.LogWarning(ex, "Watchmode quota exceeded during full sync");
                    return await PauseAsync(run.Id, page, catalogSourceId, cancellationToken);
                }

                if (catalogPage.TotalPages > 0)
                {
                    totalPages = catalogPage.TotalPages;
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
                    baselineCompleted,
                    cancellationToken);
                titlesAdded += upsert.TitlesAdded;
                titlesUpdated += upsert.TitlesUpdated;

                var tracked = await syncRunStore.GetAsync(run.Id, cancellationToken);
                if (tracked is not null)
                {
                    tracked.PagesFetched = page;
                    tracked.TitlesAdded = titlesAdded;
                    tracked.TitlesUpdated = titlesUpdated;
                    tracked.CheckpointJson = JsonSerializer.Serialize(new { page = page + 1 }, JsonOptions);
                    await syncRunStore.UpdateAsync(tracked, cancellationToken);
                }

                if (page >= totalPages || catalogPage.Entries.Count == 0)
                {
                    break;
                }
            }

            // Removals only after a complete successful page walk (NFR / FR-CAT-10).
            var removalResult = await ApplyRemovalsAsync(
                catalogSourceId,
                run.Id,
                runStartedAt,
                cancellationToken);

            if (removalResult.NeedsReview)
            {
                await syncRunStore.MarkNeedsReviewAsync(
                    run.Id,
                    $"Removal ratio {removalResult.Ratio:P1} exceeds max {removalResult.MaxRatio:P0}",
                    cancellationToken);
                var review = await syncRunStore.GetAsync(run.Id, cancellationToken);
                return ToResult(review!);
            }

            if (!baselineCompleted)
            {
                await MarkBaselineAsync(catalogSourceId, runStartedAt, cancellationToken);
                source.BaselineCompletedAt = clock.UtcNow;
            }

            source.LastFullAt = clock.UtcNow;
            source.LastFullTotalPages = totalPages;
            await db.SaveChangesAsync(cancellationToken);

            var final = await syncRunStore.GetAsync(run.Id, cancellationToken);
            if (final is not null)
            {
                final.TitlesRemoved = removalResult.Removed;
                await syncRunStore.UpdateAsync(final, cancellationToken);
            }

            await syncRunStore.SucceedAsync(run.Id, cancellationToken);
            var succeeded = await syncRunStore.GetAsync(run.Id, cancellationToken);
            return ToResult(succeeded!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Full sync failed for source {SourceId}", catalogSourceId);
            await syncRunStore.FailAsync(run.Id, ex.Message, cancellationToken);
            var failed = await syncRunStore.GetAsync(run.Id, cancellationToken);
            return ToResult(failed!);
        }
        finally
        {
            syncRunAccessor.CurrentSyncRunId = null;
        }
    }

    private async Task<CatalogSyncResult> PauseAsync(
        Guid runId,
        int page,
        int catalogSourceId,
        CancellationToken cancellationToken)
    {
        var checkpoint = JsonSerializer.Serialize(new { page }, JsonOptions);
        await syncRunStore.PauseAsync(runId, checkpoint, cancellationToken);
        logger.LogInformation("Full sync paused for source {SourceId} at page {Page}", catalogSourceId, page);
        var paused = await syncRunStore.GetAsync(runId, cancellationToken);
        return ToResult(paused!);
    }

    private async Task<RemovalPassResult> ApplyRemovalsAsync(
        int catalogSourceId,
        Guid syncRunId,
        DateTime runStartedAt,
        CancellationToken cancellationToken)
    {
        var maxRatio = await GetDoubleAsync(SettingsKeys.CatalogMaxRemovalRatio, 0.10, cancellationToken);
        var confirmations = await GetIntAsync(SettingsKeys.CatalogRemovalConfirmations, 2, cancellationToken);

        var active = await db.Availabilities
            .Where(a => a.CatalogSourceId == catalogSourceId && a.RemovedAt == null)
            .ToListAsync(cancellationToken);

        if (active.Count == 0)
        {
            return new RemovalPassResult(0, 0, maxRatio, NeedsReview: false);
        }

        var unseen = active.Where(a => a.LastSeenAt < runStartedAt).ToList();
        var ratio = unseen.Count / (double)active.Count;
        if (ratio > maxRatio)
        {
            return new RemovalPassResult(unseen.Count, ratio, maxRatio, NeedsReview: true);
        }

        var removed = 0;
        var now = clock.UtcNow;
        foreach (var availability in unseen)
        {
            availability.MissedFullScans++;
            if (availability.MissedFullScans >= confirmations)
            {
                availability.RemovedAt = now;
                db.AvailabilityEvents.Add(new AvailabilityEvent
                {
                    AvailabilityId = availability.Id,
                    Type = AvailabilityEventTypes.Removed,
                    OccurredAt = now,
                    SyncRunId = syncRunId,
                });
                removed++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new RemovalPassResult(removed, ratio, maxRatio, NeedsReview: false);
    }

    private async Task MarkBaselineAsync(int catalogSourceId, DateTime runStartedAt, CancellationToken cancellationToken)
    {
        var createdThisRun = await db.Availabilities
            .Where(a => a.CatalogSourceId == catalogSourceId && a.FirstSeenAt >= runStartedAt)
            .ToListAsync(cancellationToken);
        foreach (var availability in createdThisRun)
        {
            availability.IsBaseline = true;
            availability.AddedAt = null;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static int? ReadCheckpointPage(string? checkpointJson)
    {
        if (string.IsNullOrWhiteSpace(checkpointJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(checkpointJson);
            if (doc.RootElement.TryGetProperty("page", out var page) && page.TryGetInt32(out var value))
            {
                return value;
            }
        }
        catch (JsonException)
        {
            // ignore invalid checkpoint
        }

        return null;
    }

    private async Task<int> GetIntAsync(string key, int defaultValue, CancellationToken cancellationToken)
    {
        var raw = await settingsStore.GetRawAsync(key, cancellationToken);
        return string.IsNullOrWhiteSpace(raw) ? defaultValue : JsonSerializer.Deserialize<int>(raw, JsonOptions);
    }

    private async Task<double> GetDoubleAsync(string key, double defaultValue, CancellationToken cancellationToken)
    {
        var raw = await settingsStore.GetRawAsync(key, cancellationToken);
        return string.IsNullOrWhiteSpace(raw) ? defaultValue : JsonSerializer.Deserialize<double>(raw, JsonOptions);
    }

    private static CatalogSyncResult ToResult(SyncRun run) =>
        new(
            run.Id,
            run.Status,
            run.PagesFetched,
            run.CreditsUsed,
            run.TitlesAdded,
            run.TitlesUpdated,
            run.CheckpointJson,
            run.ErrorMessage);

    private sealed record RemovalPassResult(int Removed, double Ratio, double MaxRatio, bool NeedsReview);
}
