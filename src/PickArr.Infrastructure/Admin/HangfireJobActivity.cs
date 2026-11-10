using Hangfire;
using Hangfire.Common;
using Hangfire.Storage.Monitoring;
using Pickarr.Infrastructure.Jobs;

namespace Pickarr.Infrastructure.Admin;

internal sealed class HangfireJobActivity
{
    public static readonly HangfireJobActivity Empty = new(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    private HangfireJobActivity(
        HashSet<string> queued,
        HashSet<string> running,
        Dictionary<string, string> failedErrors,
        HashSet<string> succeeded)
    {
        Queued = queued;
        Running = running;
        FailedErrors = failedErrors;
        Succeeded = succeeded;
    }

    public HashSet<string> Queued { get; }

    public HashSet<string> Running { get; }

    public Dictionary<string, string> FailedErrors { get; }

    public HashSet<string> Succeeded { get; }

    public bool IsActive(string jobKey) =>
        Running.Contains(jobKey) || Queued.Contains(jobKey);

    public static HangfireJobActivity Read()
    {
        try
        {
            var api = JobStorage.Current.GetMonitoringApi();
            var queued = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var failedAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            var failedErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var succeededAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

            foreach (var queue in api.Queues())
            {
                foreach (var entry in api.EnqueuedJobs(queue.Name, 0, 100))
                {
                    var key = HangfireJobKeyMapper.Resolve(entry.Value.Job);
                    if (key is not null)
                    {
                        queued.Add(key);
                    }
                }
            }

            foreach (var entry in api.ProcessingJobs(0, 100))
            {
                var key = HangfireJobKeyMapper.Resolve(entry.Value.Job);
                if (key is not null)
                {
                    running.Add(key);
                }
            }

            foreach (var entry in api.FailedJobs(0, 50))
            {
                var key = HangfireJobKeyMapper.Resolve(entry.Value.Job);
                if (key is null || failedAt.ContainsKey(key))
                {
                    continue;
                }

                failedAt[key] = entry.Value.FailedAt ?? DateTime.MinValue;
                if (!string.IsNullOrWhiteSpace(entry.Value.ExceptionMessage))
                {
                    failedErrors[key] = entry.Value.ExceptionMessage;
                }
            }

            foreach (var entry in api.SucceededJobs(0, 50))
            {
                var key = HangfireJobKeyMapper.Resolve(entry.Value.Job);
                if (key is null || succeededAt.ContainsKey(key))
                {
                    continue;
                }

                succeededAt[key] = entry.Value.SucceededAt ?? DateTime.MinValue;
            }

            var succeeded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var latestFailures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, at) in failedAt)
            {
                if (succeededAt.TryGetValue(key, out var successAt) && successAt >= at)
                {
                    continue;
                }

                if (failedErrors.TryGetValue(key, out var error))
                {
                    latestFailures[key] = error;
                }
            }

            foreach (var (key, at) in succeededAt)
            {
                if (failedAt.TryGetValue(key, out var failAt) && failAt > at)
                {
                    continue;
                }

                succeeded.Add(key);
            }

            return new HangfireJobActivity(queued, running, latestFailures, succeeded);
        }
        catch (Exception)
        {
            return Empty;
        }
    }
}

internal static class HangfireJobKeyMapper
{
    public static string? Resolve(Job? job)
    {
        if (job?.Type is null)
        {
            return null;
        }

        var name = job.Type.Name;
        var sourceId = job.Args.Count > 0 ? AsInt(job.Args[0]) : null;
        if (name == nameof(CatalogIncrementalSyncJob) && sourceId is int incrementalId)
        {
            return ScheduleReconciler.IncrementalJobId(incrementalId);
        }

        if (name == nameof(CatalogFullSyncJob) && sourceId is int fullId)
        {
            return ScheduleReconciler.FullJobId(fullId);
        }

        if (name == nameof(LeavingSyncJob))
        {
            var country = AsString(job.Args.Count > 0 ? job.Args[0] : null);
            if (!string.IsNullOrWhiteSpace(country))
            {
                return LeavingSyncJob.JobId(country);
            }
        }

        if (name == nameof(CatalogResumeJob))
        {
            return ScheduleReconciler.ResumeJobId;
        }

        if (name == nameof(CatalogBootstrapJob))
        {
            return CatalogBootstrapJob.JobKey;
        }

        if (name == nameof(EnrichmentJob))
        {
            return EnrichmentJob.RecurringJobId;
        }

        if (name == nameof(MetadataRefreshJob))
        {
            return MetadataRefreshJob.RecurringJobId;
        }

        if (name == nameof(ArrSyncJob))
        {
            return job.Method.Name switch
            {
                nameof(ArrSyncJob.ExecuteSonarrAsync) => ArrSyncJob.SonarrJobId,
                nameof(ArrSyncJob.ExecuteRadarrAsync) => ArrSyncJob.RadarrJobId,
                _ => null,
            };
        }

        if (name == nameof(RecoFeatureJob))
        {
            return RecoFeatureJob.RecurringJobId;
        }

        if (name == nameof(RecoEmbeddingJob))
        {
            return RecoEmbeddingJob.RecurringJobId;
        }

        if (name == nameof(RecoBuildJob))
        {
            return RecoBuildJob.RecurringJobId;
        }

        if (name == nameof(RecoEvalJob))
        {
            return RecoEvalJob.JobKey;
        }

        if (name == nameof(RecoModelDownloadJob))
        {
            return RecoModelDownloadJob.JobKey;
        }

        return null;
    }

    private static int? AsInt(object? value) => value switch
    {
        int i => i,
        long l => checked((int)l),
        short s => s,
        string text when int.TryParse(text, out var parsed) => parsed,
        _ => null,
    };

    private static string? AsString(object? value) => value switch
    {
        string text => text,
        _ => value?.ToString(),
    };
}
