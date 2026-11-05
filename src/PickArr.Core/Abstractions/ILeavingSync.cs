namespace PickArr.Core.Abstractions;

public interface ILeavingSync
{
    Task<CatalogSyncResult> RunAsync(
        int catalogSourceId,
        bool isManual = false,
        CancellationToken cancellationToken = default);
}
