using Hangfire;
using Microsoft.Extensions.Logging;
using PickArr.Core.Abstractions;

namespace PickArr.Infrastructure.Jobs;

public sealed class LeavingSyncJob(
    ILeavingSync sync,
    ILogger<LeavingSyncJob> logger)
{
    public const string Cron = "30 5 * * *";

    [DisableConcurrentExecution(timeoutInSeconds: 60 * 30)]
    public async Task ExecuteAsync(int catalogSourceId, bool isManual = false, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting leaving sync for source {SourceId}", catalogSourceId);
        var result = await sync.RunAsync(catalogSourceId, isManual, cancellationToken);
        logger.LogInformation(
            "Leaving sync finished for source {SourceId} with status {Status}",
            catalogSourceId,
            result.Status);
    }

    public static string JobId(int sourceId) => $"catalog-leaving-{sourceId}";
}
