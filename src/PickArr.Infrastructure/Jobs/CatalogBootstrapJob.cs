using Hangfire;
using Microsoft.Extensions.Logging;

namespace Pickarr.Infrastructure.Jobs;

public sealed class CatalogBootstrapJob(
    CatalogBootstrap bootstrap,
    ILogger<CatalogBootstrapJob> logger)
{
    public const string JobKey = "catalog-bootstrap";

    [DisableConcurrentExecution(timeoutInSeconds: 60 * 10)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting initial catalog import");
        await bootstrap.ImportAsync(cancellationToken);
        logger.LogInformation("Initial catalog import finished queueing country imports");
    }
}
