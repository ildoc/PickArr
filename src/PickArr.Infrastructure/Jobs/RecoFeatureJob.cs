using Hangfire;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;

namespace Pickarr.Infrastructure.Jobs;

/// <summary>
/// Backfill keywords / credits / en-US for Ready pool titles (FR-REC-CD-04, §11.2).
/// </summary>
public sealed class RecoFeatureJob(
    IEnrichmentService enrichment,
    ISyncRunStore syncRuns,
    ILogger<RecoFeatureJob> logger)
{
    public const string RecurringJobId = "reco-feature";

    [Queue("ml")]
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    public async Task ExecuteAsync(bool isManual = false, CancellationToken cancellationToken = default)
    {
        var trigger = isManual ? SyncRunTriggers.Manual : SyncRunTriggers.Schedule;
        var run = await syncRuns.StartAsync(
            SyncRunKinds.RecoFeature,
            catalogSourceId: null,
            trigger,
            cancellationToken: cancellationToken);

        try
        {
            var totalProcessed = 0;
            var totalReady = 0;
            EnrichmentBatchResult? last = null;
            // Cap batches so a single run cannot stall the ml queue forever on huge pools.
            for (var i = 0; i < 20; i++)
            {
                last = await enrichment.EnrichFeatureBackfillBatchAsync(50, cancellationToken);
                totalProcessed += last.Processed;
                totalReady += last.Ready;
                if (last.Processed == 0)
                {
                    break;
                }
            }

            var tracked = await syncRuns.GetAsync(run.Id, cancellationToken);
            if (tracked is not null)
            {
                tracked.TitlesUpdated = totalReady;
                tracked.TitlesAdded = totalProcessed;
                if (!string.IsNullOrWhiteSpace(last?.LastError))
                {
                    tracked.ErrorMessage = last.LastError;
                }

                await syncRuns.UpdateAsync(tracked, cancellationToken);
            }

            await syncRuns.SucceedAsync(run.Id, cancellationToken);
            logger.LogInformation(
                "Reco feature backfill processed {Processed} titles ({Ready} ready)",
                totalProcessed,
                totalReady);
        }
        catch (Exception ex)
        {
            await syncRuns.FailAsync(run.Id, ex.Message, cancellationToken);
            throw;
        }
    }
}
