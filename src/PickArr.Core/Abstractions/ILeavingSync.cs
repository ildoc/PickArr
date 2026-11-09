namespace PickArr.Core.Abstractions;

public interface ILeavingSync
{
    /// <summary>
    /// Syncs leaving dates for every enabled catalog source in <paramref name="countryCode"/>
    /// with a single Watchmode call (FR-CAT-11).
    /// </summary>
    Task<CatalogSyncResult> RunForCountryAsync(
        string countryCode,
        bool isManual = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the source's country and delegates to <see cref="RunForCountryAsync"/>.
    /// </summary>
    Task<CatalogSyncResult> RunAsync(
        int catalogSourceId,
        bool isManual = false,
        CancellationToken cancellationToken = default);
}
