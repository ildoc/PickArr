using System.Reflection;
using Hangfire.Common;
using PickArr.Core.Admin;
using PickArr.Core.Domain;
using PickArr.Infrastructure.Admin;
using PickArr.Infrastructure.Jobs;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

public sealed class JobStateTests
{
    [Fact]
    public void Resolve_distinguishes_queue_running_and_failure_log()
    {
        var running = JobExecutionStateResolver.Resolve(
            SyncRunStatuses.Failed,
            "old failure",
            hangfireProcessing: true,
            hangfireQueued: false,
            hangfireFailedError: "old failure",
            hangfireSucceeded: false,
            hasCron: true);
        running.State.ShouldBe(JobExecutionStates.Running);
        running.Error.ShouldBeNull();

        var queued = JobExecutionStateResolver.Resolve(
            SyncRunStatuses.Failed,
            "quota exceeded",
            hangfireProcessing: false,
            hangfireQueued: true,
            hangfireFailedError: null,
            hangfireSucceeded: false,
            hasCron: true);
        queued.State.ShouldBe(JobExecutionStates.Queued);
        queued.Error.ShouldBe("quota exceeded");

        var failed = JobExecutionStateResolver.Resolve(
            SyncRunStatuses.Failed,
            "provider timeout",
            hangfireProcessing: false,
            hangfireQueued: false,
            hangfireFailedError: null,
            hangfireSucceeded: false,
            hasCron: true);
        failed.State.ShouldBe(JobExecutionStates.Failed);
        failed.Error.ShouldBe("provider timeout");

        var scheduled = JobExecutionStateResolver.Resolve(
            latestRunStatus: null,
            latestRunError: null,
            hangfireProcessing: false,
            hangfireQueued: false,
            hangfireFailedError: null,
            hangfireSucceeded: false,
            hasCron: true);
        scheduled.State.ShouldBe(JobExecutionStates.Scheduled);
        scheduled.Error.ShouldBeNull();
    }

    [Fact]
    public void Resolve_maps_hangfire_jobs_back_to_stable_keys()
    {
        HangfireJobKeyMapper.Resolve(JobFor(typeof(CatalogIncrementalSyncJob), nameof(CatalogIncrementalSyncJob.ExecuteAsync), 4))
            .ShouldBe("catalog-incr-4");
        HangfireJobKeyMapper.Resolve(JobFor(typeof(CatalogFullSyncJob), nameof(CatalogFullSyncJob.ExecuteAsync), 8))
            .ShouldBe("catalog-full-8");
        HangfireJobKeyMapper.Resolve(JobFor(typeof(LeavingSyncJob), nameof(LeavingSyncJob.ExecuteAsync), 3))
            .ShouldBe("catalog-leaving-3");
        HangfireJobKeyMapper.Resolve(JobFor(typeof(CatalogResumeJob), nameof(CatalogResumeJob.ExecuteAsync)))
            .ShouldBe("catalog-resume");
        HangfireJobKeyMapper.Resolve(JobFor(typeof(CatalogBootstrapJob), nameof(CatalogBootstrapJob.ExecuteAsync)))
            .ShouldBe("catalog-bootstrap");
        HangfireJobKeyMapper.Resolve(JobFor(typeof(ArrSyncJob), nameof(ArrSyncJob.ExecuteSonarrAsync)))
            .ShouldBe("arr-sync-sonarr");
        HangfireJobKeyMapper.Resolve(JobFor(typeof(ArrSyncJob), nameof(ArrSyncJob.ExecuteRadarrAsync)))
            .ShouldBe("arr-sync-radarr");
    }

    private static Job JobFor(Type type, string methodName, int? sourceId = null)
    {
        var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Missing {type.Name}.{methodName}");
        var args = method.GetParameters()
            .Select((parameter, index) =>
            {
                if (index == 0 && sourceId is int id && parameter.ParameterType == typeof(int))
                {
                    return (object)id;
                }

                if (parameter.ParameterType == typeof(bool))
                {
                    return false;
                }

                if (parameter.ParameterType == typeof(string))
                {
                    return null!;
                }

                if (parameter.ParameterType == typeof(CancellationToken))
                {
                    return CancellationToken.None;
                }

                return parameter.HasDefaultValue ? parameter.DefaultValue! : null!;
            })
            .ToArray();
        return new Job(type, method, args);
    }
}
