using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;
using PickArr.Infrastructure.Persistence;

namespace PickArr.Infrastructure.Jobs;

/// <summary>
/// Resumes Paused catalog sync runs (budget/quota) by re-queuing full/incremental work.
/// </summary>
public sealed class CatalogResumeJob(
    PickArrDbContext db,
    IBackgroundJobClient backgroundJobs,
    ILogger<CatalogResumeJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 30)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var paused = await db.SyncRuns.AsNoTracking()
            .Where(r => r.Status == SyncRunStatuses.Paused && r.CatalogSourceId != null)
            .OrderBy(r => r.StartedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var run in paused)
        {
            var sourceId = run.CatalogSourceId!.Value;
            if (run.Kind == SyncRunKinds.CatalogFull)
            {
                backgroundJobs.Enqueue<CatalogFullSyncJob>(j =>
                    j.ExecuteAsync(sourceId, false, run.CheckpointJson, CancellationToken.None));
                logger.LogInformation("Enqueued full sync resume for source {SourceId} from run {RunId}", sourceId, run.Id);
            }
            else if (run.Kind == SyncRunKinds.CatalogIncremental)
            {
                backgroundJobs.Enqueue<CatalogIncrementalSyncJob>(j =>
                    j.ExecuteAsync(sourceId, false, CancellationToken.None));
                logger.LogInformation("Enqueued incremental sync resume for source {SourceId} from run {RunId}", sourceId, run.Id);
            }
        }
    }
}
