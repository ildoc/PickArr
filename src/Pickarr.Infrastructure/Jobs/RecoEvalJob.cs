using Hangfire;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;

namespace Pickarr.Infrastructure.Jobs;

public sealed class RecoEvalJob(
    IRecoEvalService eval,
    ISyncRunStore syncRuns,
    ILogger<RecoEvalJob> logger)
{
    [Queue("ml")]
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 30)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var run = await syncRuns.StartAsync(
            SyncRunKinds.RecoEval,
            catalogSourceId: null,
            SyncRunTriggers.Manual,
            cancellationToken: cancellationToken);

        try
        {
            var report = await eval.EvaluateAsync(cancellationToken);
            var tracked = await syncRuns.GetAsync(run.Id, cancellationToken);
            if (tracked is not null)
            {
                tracked.TitlesUpdated = report.RatingCount;
                await syncRuns.UpdateAsync(tracked, cancellationToken);
            }

            await syncRuns.SucceedAsync(run.Id, cancellationToken);
            logger.LogInformation(
                "Reco eval done ratings={Ratings} l3Active={L3} warning={Warning}",
                report.RatingCount,
                report.Level3Active,
                report.LowSampleWarning);
        }
        catch (Exception ex)
        {
            await syncRuns.FailAsync(run.Id, ex.Message, cancellationToken);
            throw;
        }
    }
}
