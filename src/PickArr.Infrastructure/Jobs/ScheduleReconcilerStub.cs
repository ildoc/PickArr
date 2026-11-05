using Hangfire;
using Microsoft.Extensions.Logging;

namespace PickArr.Infrastructure.Jobs;

/// <summary>
/// Spike stub for SP-4: demonstrates that recurring schedules are owned by the app DB
/// and re-registered into Hangfire on startup / settings change (T12 will read CatalogSources).
/// </summary>
public sealed class ScheduleReconcilerStub(
    IRecurringJobManager recurringJobs,
    ILogger<ScheduleReconcilerStub> logger)
{
    public const string ProbeJobId = "pickarr:spike-probe";

    public void ReconcileProbe(string cronExpression = "0 5 * * *")
    {
        recurringJobs.AddOrUpdate<SpikeProbeJob>(
            ProbeJobId,
            job => job.Execute(),
            cronExpression,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Local,
            });

        logger.LogInformation("ScheduleReconcilerStub registered {JobId} with cron {Cron}", ProbeJobId, cronExpression);
    }
}

public sealed class SpikeProbeJob(ILogger<SpikeProbeJob> logger)
{
    public void Execute() => logger.LogDebug("Spike probe job tick");
}
