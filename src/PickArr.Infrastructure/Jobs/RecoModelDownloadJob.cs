using Hangfire;
using Microsoft.Extensions.Logging;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;

namespace PickArr.Infrastructure.Jobs;

public sealed class RecoModelDownloadJob(
    IRecoModelService models,
    ISyncRunStore syncRuns,
    ILogger<RecoModelDownloadJob> logger)
{
    [Queue("ml")]
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    public async Task ExecuteAsync(bool detectOnly = false, CancellationToken cancellationToken = default)
    {
        var run = await syncRuns.StartAsync(
            SyncRunKinds.RecoModelDownload,
            catalogSourceId: null,
            SyncRunTriggers.Manual,
            cancellationToken: cancellationToken);

        try
        {
            if (detectOnly)
            {
                await models.DetectAsync(cancellationToken);
            }
            else
            {
                await models.DownloadAsync(cancellationToken);
            }

            await syncRuns.SucceedAsync(run.Id, cancellationToken);
            logger.LogInformation("Reco model {Action} finished", detectOnly ? "detect" : "download");
        }
        catch (Exception ex)
        {
            await syncRuns.FailAsync(run.Id, ex.Message, cancellationToken);
            throw;
        }
    }
}
