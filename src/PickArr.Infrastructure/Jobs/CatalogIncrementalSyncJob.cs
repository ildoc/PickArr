using Hangfire;
using Microsoft.Extensions.Logging;
using PickArr.Core.Abstractions;

namespace PickArr.Infrastructure.Jobs;

public sealed class CatalogIncrementalSyncJob(
    ICatalogIncrementalSync sync,
    ILogger<CatalogIncrementalSyncJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    public async Task ExecuteAsync(int catalogSourceId, bool isManual = false, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting incremental catalog sync for source {SourceId}", catalogSourceId);
        var result = await sync.RunAsync(catalogSourceId, isManual, cancellationToken);
        logger.LogInformation(
            "Incremental catalog sync finished for source {SourceId} with status {Status}",
            catalogSourceId,
            result.Status);
    }
}
