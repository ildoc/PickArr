using Hangfire;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;

namespace Pickarr.Infrastructure.Jobs;

public sealed class RecoEmbeddingJob(
    IRecoEmbeddingService embeddings,
    IBackgroundJobClient jobs,
    ISyncRunStore syncRuns,
    ILogger<RecoEmbeddingJob> logger)
{
    public const string RecurringJobId = "reco-embedding";

    [Queue("ml")]
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60 * 6)]
    public async Task ExecuteAsync(bool rebuildAll = false, bool isManual = false, CancellationToken cancellationToken = default)
    {
        var trigger = isManual ? SyncRunTriggers.Manual : SyncRunTriggers.Schedule;
        var run = await syncRuns.StartAsync(
            SyncRunKinds.RecoEmbedding,
            catalogSourceId: null,
            trigger,
            cancellationToken: cancellationToken);

        try
        {
            var count = await embeddings.EmbedPendingAsync(rebuildAll, cancellationToken);
            var tracked = await syncRuns.GetAsync(run.Id, cancellationToken);
            if (tracked is not null)
            {
                tracked.TitlesUpdated = count;
                await syncRuns.UpdateAsync(tracked, cancellationToken);
            }

            await syncRuns.SucceedAsync(run.Id, cancellationToken);
            logger.LogInformation("Reco embeddings processed {Count} titles", count);

            if (count > 0)
            {
                jobs.Enqueue<RecoBuildJob>(j => j.ExecuteAsync(null, false, CancellationToken.None));
            }
        }
        catch (Exception ex)
        {
            await syncRuns.FailAsync(run.Id, ex.Message, cancellationToken);
            throw;
        }
    }
}
