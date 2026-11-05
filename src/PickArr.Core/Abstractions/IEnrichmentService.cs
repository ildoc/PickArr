namespace PickArr.Core.Abstractions;

public sealed record EnrichmentBatchResult(
    int Processed,
    int Ready,
    int Failed,
    int Unmapped,
    string? LastError = null);

public interface IEnrichmentService
{
    Task EnrichTitleAsync(int titleId, CancellationToken cancellationToken = default);

    Task<EnrichmentBatchResult> EnrichPendingBatchAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<EnrichmentBatchResult> RefreshStaleBatchAsync(
        int limit = 50,
        int olderThanDays = 30,
        CancellationToken cancellationToken = default);
}
