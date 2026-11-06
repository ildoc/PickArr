using Hangfire;
using Microsoft.Extensions.Logging;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;

namespace PickArr.Infrastructure.Jobs;

public sealed class RecoBuildJob(
    IRecoBuildService build,
    ISyncRunStore syncRuns,
    ILogger<RecoBuildJob> logger)
{
    public const string RecurringJobId = "reco-build";

    [Queue("ml")]
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 30)]
    public async Task ExecuteAsync(bool isManual = false, CancellationToken cancellationToken = default)
    {
        var trigger = isManual ? SyncRunTriggers.Manual : SyncRunTriggers.Schedule;
        var run = await syncRuns.StartAsync(
            SyncRunKinds.RecoBuild,
            catalogSourceId: null,
            trigger,
            cancellationToken: cancellationToken);

        try
        {
            var version = await build.BuildAsync(cancellationToken);
            var tracked = await syncRuns.GetAsync(run.Id, cancellationToken);
            if (tracked is not null)
            {
                tracked.TitlesUpdated = version;
                await syncRuns.UpdateAsync(tracked, cancellationToken);
            }

            await syncRuns.SucceedAsync(run.Id, cancellationToken);
            logger.LogInformation("Reco build published version {Version}", version);
        }
        catch (Exception ex)
        {
            await syncRuns.FailAsync(run.Id, ex.Message, cancellationToken);
            throw;
        }
    }
}
