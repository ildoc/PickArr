using PickArr.Core.Admin;
using PickArr.Core.Domain;

namespace PickArr.Infrastructure.Admin;

internal static class JobExecutionStateResolver
{
    public static (string State, string? Error) Resolve(
        string? latestRunStatus,
        string? latestRunError,
        bool hangfireProcessing,
        bool hangfireQueued,
        string? hangfireFailedError,
        bool hangfireSucceeded,
        bool hasCron)
    {
        string? previousError = null;
        if (latestRunStatus is SyncRunStatuses.Failed or SyncRunStatuses.NeedsReview or SyncRunStatuses.Paused)
        {
            previousError = string.IsNullOrWhiteSpace(latestRunError) ? hangfireFailedError : latestRunError;
        }
        else if (latestRunStatus is null && !string.IsNullOrWhiteSpace(hangfireFailedError))
        {
            previousError = hangfireFailedError;
        }

        if (hangfireProcessing || latestRunStatus == SyncRunStatuses.Running)
        {
            return (JobExecutionStates.Running, null);
        }

        if (hangfireQueued)
        {
            return (JobExecutionStates.Queued, previousError);
        }

        var state = latestRunStatus switch
        {
            SyncRunStatuses.Failed => JobExecutionStates.Failed,
            SyncRunStatuses.NeedsReview => JobExecutionStates.NeedsReview,
            SyncRunStatuses.Paused => JobExecutionStates.Paused,
            SyncRunStatuses.Succeeded => JobExecutionStates.Succeeded,
            SyncRunStatuses.PartiallySucceeded => JobExecutionStates.PartiallySucceeded,
            _ when latestRunStatus is null && !string.IsNullOrWhiteSpace(hangfireFailedError) => JobExecutionStates.Failed,
            _ when latestRunStatus is null && hangfireSucceeded => JobExecutionStates.Succeeded,
            _ when hasCron => JobExecutionStates.Scheduled,
            _ => JobExecutionStates.Idle,
        };

        var showError = state is JobExecutionStates.Failed or JobExecutionStates.NeedsReview or JobExecutionStates.Paused;
        return (state, showError ? previousError : null);
    }
}
