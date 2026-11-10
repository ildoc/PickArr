using Hangfire;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;

namespace Pickarr.Infrastructure.Jobs;

public sealed class LeavingSyncJob(
    ILeavingSync sync,
    ILogger<LeavingSyncJob> logger)
{
    public const string Cron = "30 5 * * *";

    [DisableConcurrentExecution(timeoutInSeconds: 60 * 30)]
    public async Task ExecuteAsync(
        string countryCode,
        bool isManual = false,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting leaving sync for country {Country}", countryCode);
        var result = await sync.RunForCountryAsync(countryCode, isManual, cancellationToken);
        logger.LogInformation(
            "Leaving sync finished for country {Country} with status {Status}",
            countryCode,
            result.Status);
    }

    public static string JobId(string countryCode) =>
        $"catalog-leaving-{countryCode.Trim().ToUpperInvariant()}";
}
