using Hangfire;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;

namespace Pickarr.Infrastructure.Jobs;

/// <summary>
/// True debounce per profile: each vote cancels that profile's previous delayed Hangfire job
/// and schedules a new one 30 seconds out. Another profile's pending job stays.
/// </summary>
public sealed class RecoRebuildScheduler(
    IBackgroundJobClient jobs,
    ILogger<RecoRebuildScheduler> logger) : IRecoRebuildScheduler
{
    public static readonly TimeSpan Debounce = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private readonly Dictionary<int, string> _pendingJobIds = [];

    public void ScheduleDebouncedRebuild(int profileId)
    {
        lock (_gate)
        {
            if (_pendingJobIds.TryGetValue(profileId, out var pendingJobId))
            {
                try
                {
                    jobs.Delete(pendingJobId);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not delete previous reco rebuild job {JobId}", pendingJobId);
                }

                _pendingJobIds.Remove(profileId);
            }

            var jobId = jobs.Schedule<RecoBuildJob>(
                j => j.ExecuteAsync(profileId, false, CancellationToken.None),
                Debounce);
            _pendingJobIds[profileId] = jobId;
            logger.LogDebug("Scheduled reco rebuild job {JobId} for profile {ProfileId} in {Delay}", jobId, profileId, Debounce);
        }
    }
}
