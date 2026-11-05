using Hangfire;
using Microsoft.Extensions.Logging;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;

namespace PickArr.Infrastructure.Jobs;

public sealed class EnrichmentJob(
    IEnrichmentService enrichment,
    ISyncRunStore syncRuns,
    ILogger<EnrichmentJob> logger)
{
    public const string RecurringJobId = "metadata-enrichment";

    [DisableConcurrentExecution(timeoutInSeconds: 60 * 30)]
    public async Task ExecuteAsync(bool isManual = false, CancellationToken cancellationToken = default)
    {
        await MetadataJobRun.ExecuteAsync(
            syncRuns,
            SyncRunKinds.Enrichment,
            isManual,
            ct => enrichment.EnrichPendingBatchAsync(cancellationToken: ct),
            maxBatches: 15,
            cancellationToken);
        logger.LogInformation("Metadata enrichment finished");
    }
}
