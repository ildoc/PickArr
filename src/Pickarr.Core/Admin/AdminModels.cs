namespace Pickarr.Core.Admin;

public static class JobExecutionStates
{
    public const string Idle = "Idle";
    public const string Scheduled = "Scheduled";
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Paused = "Paused";
    public const string PartiallySucceeded = "PartiallySucceeded";
    public const string NeedsReview = "NeedsReview";
}

public sealed record AdminJobDto(
    string JobKey,
    string DisplayName,
    string Kind,
    int? SourceId,
    string? CountryCode,
    string? ProviderName,
    string? Cron,
    DateTime? LastExecution,
    DateTime? NextExecution,
    string? LastOutcome,
    Guid? LastRunId,
    bool CanTrigger,
    string ExecutionState,
    string? LastError,
    string Description);

public sealed record TriggerJobRequest(int? SourceId, string? Mode);

public sealed record TriggerJobResult(Guid? RunId, string HangfireJobId);

public sealed record AdminRunSummaryDto(
    Guid Id,
    string Kind,
    int? SourceId,
    string? CountryCode,
    string TriggeredBy,
    string Status,
    DateTime StartedAt,
    DateTime? FinishedAt,
    int CreditsUsed,
    int PagesFetched,
    int TitlesAdded,
    int TitlesUpdated,
    int TitlesRemoved,
    string? ErrorMessage);

public sealed record PagedAdminRunsDto(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<AdminRunSummaryDto> Items);

public sealed record AdminQuotaDto(
    string Service,
    int Quota,
    int Used,
    int Reserve,
    int Spendable,
    int? ProjectedMonthEnd,
    DateTime? LastUpdatedAt,
    DateTime PeriodStart,
    DateTime PeriodEnd);

public sealed class SyncAlreadyRunningException : Exception
{
    public SyncAlreadyRunningException(string kind, int? sourceId)
        : base(sourceId is null
            ? $"A '{kind}' run is already in progress."
            : $"A '{kind}' run for source {sourceId} is already in progress.")
    {
        Kind = kind;
        SourceId = sourceId;
    }

    public string Kind { get; }
    public int? SourceId { get; }
}
