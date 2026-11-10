namespace Pickarr.Core.Abstractions;

public sealed record CatalogSyncResult(
    Guid SyncRunId,
    string Status,
    int PagesFetched,
    int CreditsUsed,
    int TitlesAdded,
    int TitlesUpdated,
    string? CheckpointJson,
    string? ErrorMessage);

public interface ICatalogIncrementalSync
{
    Task<CatalogSyncResult> RunAsync(
        int catalogSourceId,
        bool isManual = false,
        string? resumeCheckpointJson = null,
        CancellationToken cancellationToken = default);
}
