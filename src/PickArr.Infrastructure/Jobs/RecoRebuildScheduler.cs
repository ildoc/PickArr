using Hangfire;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;

namespace Pickarr.Infrastructure.Jobs;

/// <summary>
/// True debounce: each vote cancels the previous delayed Hangfire job and schedules a new one
/// 30 seconds out. Concurrent builds are still gated by <see cref="DisableConcurrentExecutionAttribute"/>
/// on <see cref="RecoBuildJob"/>.
/// </summary>
public sealed class RecoRebuildScheduler(
    IBackgroundJobClient jobs,
    ILogger<RecoRebuildScheduler> logger) : IRecoRebuildScheduler
{
    public static readonly TimeSpan Debounce = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private string? _pendingJobId;

    public void ScheduleDebouncedRebuild()
    {
        lock (_gate)
        {
            if (_pendingJobId is not null)
            {
                try
                {
                    jobs.Delete(_pendingJobId);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Could not delete previous reco rebuild job {JobId}", _pendingJobId);
                }

                _pendingJobId = null;
            }

            _pendingJobId = jobs.Schedule<RecoBuildJob>(
                j => j.ExecuteAsync(false, CancellationToken.None),
                Debounce);
            logger.LogDebug("Scheduled reco rebuild job {JobId} in {Delay}", _pendingJobId, Debounce);
        }
    }
}
