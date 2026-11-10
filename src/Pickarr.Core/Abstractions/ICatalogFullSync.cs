namespace Pickarr.Core.Abstractions;

public interface ICatalogFullSync
{
    Task<CatalogSyncResult> RunAsync(
        int catalogSourceId,
        bool isManual = false,
        string? resumeCheckpointJson = null,
        CancellationToken cancellationToken = default);
}
