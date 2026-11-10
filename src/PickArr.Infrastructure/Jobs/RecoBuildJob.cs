using Hangfire;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;

namespace Pickarr.Infrastructure.Jobs;

public sealed class RecoBuildJob(
    IRecoBuildService build,
    ISyncRunStore syncRuns,
    ILogger<RecoBuildJob> logger)
{
    public const string RecurringJobId = "reco-build";

    [Queue("ml")]
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 30)]
    public async Task ExecuteAsync(int? profileId, bool isManual, CancellationToken cancellationToken = default)
    {
        var trigger = isManual ? SyncRunTriggers.Manual : SyncRunTriggers.Schedule;
        var run = await syncRuns.StartAsync(
            SyncRunKinds.RecoBuild,
            catalogSourceId: null,
            trigger,
            cancellationToken: cancellationToken);

        try
        {
            var version = profileId is int id
                ? await build.BuildAsync(id, cancellationToken)
                : await build.BuildAsync(cancellationToken);
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
