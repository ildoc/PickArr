using Hangfire;
using Microsoft.Extensions.Logging;
using PickArr.Core.Abstractions;

namespace PickArr.Infrastructure.Jobs;

public sealed class CatalogFullSyncJob(
    ICatalogFullSync sync,
    ILogger<CatalogFullSyncJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60 * 6)]
    public async Task ExecuteAsync(
        int catalogSourceId,
        bool isManual = false,
        string? resumeCheckpointJson = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting full catalog sync for source {SourceId}", catalogSourceId);
        var result = await sync.RunAsync(catalogSourceId, isManual, resumeCheckpointJson, cancellationToken);
        logger.LogInformation(
            "Full catalog sync finished for source {SourceId} with status {Status}",
            catalogSourceId,
            result.Status);
    }
}
