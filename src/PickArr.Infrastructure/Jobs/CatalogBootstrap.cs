using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Admin;
using PickArr.Infrastructure.Persistence;

namespace PickArr.Infrastructure.Jobs;

/// <summary>
/// Enables the seeded country catalogs and queues a first import so the grid is not empty
/// after the Watchmode API key is saved.
/// </summary>
public sealed class CatalogBootstrap(
    PickArrDbContext db,
    ISettingsStore settings,
    IBackgroundJobClient backgroundJobs,
    ScheduleReconciler reconciler,
    IOptions<CatalogBootstrapOptions> options,
    ILogger<CatalogBootstrap> logger)
{
    public async Task<string?> StartAsync(bool force, CancellationToken cancellationToken = default)
    {
        await RecoverInterruptedRunsAsync(cancellationToken);

        if (!force && !options.Value.AutoInitialImport)
        {
            return null;
        }

        var keyReady = await settings.IsSetAsync(SettingsKeys.WatchmodeApiKey, cancellationToken);
        if (!keyReady)
        {
            if (force)
            {
                throw new ArgumentException("Save a Watchmode API key before running the initial catalog import.");
            }

            return null;
        }

        if (!force && await IsMarkedAsync(cancellationToken) && !await HasPendingImportAsync(cancellationToken))
        {
            return null;
        }

        await reconciler.ReconcileAsync(cancellationToken);
        var jobId = backgroundJobs.Enqueue<CatalogBootstrapJob>(j => j.ExecuteAsync(CancellationToken.None));
        if (!force)
        {
            await settings.SetAsync(SettingsKeys.CatalogInitialImportStarted, "true", isSecret: false, cancellationToken);
        }

        logger.LogInformation("Queued initial catalog import {JobId}", jobId);
        return jobId;
    }

    public async Task ImportAsync(CancellationToken cancellationToken = default)
    {
        if (!await settings.IsSetAsync(SettingsKeys.WatchmodeApiKey, cancellationToken))
        {
            logger.LogWarning("Skipping initial catalog import because the Watchmode API key is not set.");
            return;
        }

        await reconciler.ReconcileAsync(cancellationToken);

        var activity = HangfireJobActivity.Read();
        var sources = await db.CatalogSources.AsNoTracking()
            .Where(s => s.IsEnabled && s.Provider != null && s.Provider.IsEnabled)
            .ToListAsync(cancellationToken);

        foreach (var source in sources)
        {
            if (!NeverImported(source))
            {
                continue;
            }

            var busy = await db.SyncRuns.AsNoTracking().AnyAsync(
                r => r.CatalogSourceId == source.Id
                     && r.Status == SyncRunStatuses.Running
                     && (r.Kind == SyncRunKinds.CatalogIncremental || r.Kind == SyncRunKinds.CatalogFull),
                cancellationToken);
            if (busy)
            {
                continue;
            }

            var incrementalKey = ScheduleReconciler.IncrementalJobId(source.Id);
            if (activity.IsActive(incrementalKey))
            {
                continue;
            }

            var sourceId = source.Id;
            backgroundJobs.Enqueue<CatalogIncrementalSyncJob>(j =>
                j.ExecuteAsync(sourceId, false, CancellationToken.None));
            logger.LogInformation(
                "Queued first catalog pages for {Country} (source {SourceId})",
                source.CountryCode,
                sourceId);
        }
    }

    private async Task RecoverInterruptedRunsAsync(CancellationToken cancellationToken)
    {
        var interrupted = await db.SyncRuns
            .Where(r => r.Status == SyncRunStatuses.Running)
            .ToListAsync(cancellationToken);
        if (interrupted.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var run in interrupted)
        {
            run.Status = SyncRunStatuses.Failed;
            run.FinishedAt = now;
            run.ErrorMessage = "Interrupted by an application restart.";
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogWarning("Marked {Count} sync run(s) as failed after an application restart", interrupted.Count);
    }

    private Task<bool> HasPendingImportAsync(CancellationToken cancellationToken) =>
        db.CatalogSources.AnyAsync(
            s => s.IsEnabled
                 && s.Provider != null
                 && s.Provider.IsEnabled
                 && s.BaselineCompletedAt == null
                 && s.LastIncrementalAt == null
                 && s.LastFullAt == null,
            cancellationToken);

    private async Task<bool> IsMarkedAsync(CancellationToken cancellationToken)
    {
        var raw = await settings.GetRawAsync(SettingsKeys.CatalogInitialImportStarted, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return string.Equals(raw.Trim().Trim('"'), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NeverImported(CatalogSource source) =>
        source.BaselineCompletedAt is null
        && source.LastIncrementalAt is null
        && source.LastFullAt is null;
}
