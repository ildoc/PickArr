namespace Pickarr.Core.Abstractions;

public interface IRecoEventLogService
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

    Task LogAsync(
        int profileId,
        int titleId,
        string eventKind,
        int? version = null,
        int? rank = null,
        string? scope = null,
        CancellationToken cancellationToken = default);

    Task LogManyAsync(
        int profileId,
        IReadOnlyList<RecoEventLogEntry> entries,
        CancellationToken cancellationToken = default);
}

public sealed record RecoEventLogEntry(
    int TitleId,
    string Event,
    int? Version = null,
    int? Rank = null,
    string? Scope = null);
