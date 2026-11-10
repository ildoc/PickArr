namespace Pickarr.Core.Abstractions;

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

    /// <summary>
    /// Re-enrich Ready pool titles missing keywords, credits, or en-US localization (RecoFeatureJob).
    /// </summary>
    Task<EnrichmentBatchResult> EnrichFeatureBackfillBatchAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);
}
