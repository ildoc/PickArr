using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PickArr.Core.Abstractions;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Persistence;
using System.Text.Json;

namespace PickArr.Infrastructure.Jobs;

public sealed class ScheduleReconciler(
    PickArrDbContext db,
    IRecurringJobManager recurringJobs,
    ISettingsStore settings,
    ILogger<ScheduleReconciler> logger)
{
    public const string ResumeJobId = "catalog-resume";

    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var sources = await db.CatalogSources.AsNoTracking()
            .Include(source => source.Provider)
            .ToListAsync(cancellationToken);
        foreach (var source in sources.Where(source => !source.IsEnabled || source.Provider?.IsEnabled != true))
        {
            recurringJobs.RemoveIfExists(IncrementalJobId(source.Id));
            recurringJobs.RemoveIfExists(FullJobId(source.Id));
            recurringJobs.RemoveIfExists(LeavingSyncJob.JobId(source.Id));
        }

        sources = sources.Where(source => source.IsEnabled && source.Provider?.IsEnabled == true).ToList();

        var incrementalCount = 0;
        var fullCount = 0;

        foreach (var source in sources)
        {
            var incrId = IncrementalJobId(source.Id);
            var fullId = FullJobId(source.Id);

            recurringJobs.AddOrUpdate<CatalogIncrementalSyncJob>(
                incrId,
                j => j.ExecuteAsync(source.Id, false, CancellationToken.None),
                source.IncrementalCron);
            incrementalCount++;

            recurringJobs.AddOrUpdate<CatalogFullSyncJob>(
                fullId,
                j => j.ExecuteAsync(source.Id, false, null, CancellationToken.None),
                source.FullSyncCron);
            fullCount++;

            recurringJobs.AddOrUpdate<LeavingSyncJob>(
                LeavingSyncJob.JobId(source.Id),
                j => j.ExecuteAsync(source.Id, false, CancellationToken.None),
                LeavingSyncJob.Cron);
        }

        recurringJobs.AddOrUpdate<CatalogResumeJob>(
            ResumeJobId,
            j => j.ExecuteAsync(CancellationToken.None),
            Cron.Daily());

        recurringJobs.AddOrUpdate<EnrichmentJob>(
            EnrichmentJob.RecurringJobId,
            j => j.ExecuteAsync(false, CancellationToken.None),
            Cron.Hourly());

        recurringJobs.AddOrUpdate<MetadataRefreshJob>(
            MetadataRefreshJob.RecurringJobId,
            j => j.ExecuteAsync(false, CancellationToken.None),
            Cron.Daily());

        var sonarrMinutes = await GetIntervalAsync(SettingsKeys.SonarrSyncIntervalMinutes, 15, cancellationToken);
        var radarrMinutes = await GetIntervalAsync(SettingsKeys.RadarrSyncIntervalMinutes, 15, cancellationToken);
        recurringJobs.AddOrUpdate<ArrSyncJob>(
            ArrSyncJob.SonarrJobId,
            j => j.ExecuteSonarrAsync(CancellationToken.None),
            $"*/{Math.Clamp(sonarrMinutes, 1, 1440)} * * * *");
        recurringJobs.AddOrUpdate<ArrSyncJob>(
            ArrSyncJob.RadarrJobId,
            j => j.ExecuteRadarrAsync(CancellationToken.None),
            $"*/{Math.Clamp(radarrMinutes, 1, 1440)} * * * *");

        logger.LogInformation(
            "ScheduleReconciler registered {Incremental} incremental, {Full} full, and leaving catalog jobs (+ resume/enrichment/refresh/arr)",
            incrementalCount,
            fullCount);
    }

    public static string IncrementalJobId(int sourceId) => $"catalog-incr-{sourceId}";
    public static string FullJobId(int sourceId) => $"catalog-full-{sourceId}";

    private async Task<int> GetIntervalAsync(string key, int defaultValue, CancellationToken cancellationToken)
    {
        var raw = await settings.GetRawAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        try
        {
            return JsonSerializer.Deserialize<int>(raw);
        }
        catch (JsonException)
        {
            return int.TryParse(raw, out var n) ? n : defaultValue;
        }
    }
}
