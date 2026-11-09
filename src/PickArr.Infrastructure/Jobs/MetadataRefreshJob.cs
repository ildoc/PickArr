using Hangfire;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;

namespace Pickarr.Infrastructure.Jobs;

public sealed class MetadataRefreshJob(
    IEnrichmentService enrichment,
    ISyncRunStore syncRuns,
    ILogger<MetadataRefreshJob> logger)
{
    public const string RecurringJobId = "metadata-refresh";

    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    public async Task ExecuteAsync(bool isManual = false, CancellationToken cancellationToken = default)
    {
        await MetadataJobRun.ExecuteAsync(
            syncRuns,
            SyncRunKinds.MetadataRefresh,
            isManual,
            ct => enrichment.RefreshStaleBatchAsync(cancellationToken: ct),
            maxBatches: 4,
            cancellationToken);
        logger.LogInformation("Metadata refresh finished");
    }
}
