using Pickarr.Core.Catalog;

namespace Pickarr.Core.Abstractions;

public interface ICatalogProvider
{
    string Type { get; }
    CatalogProviderCapabilities Capabilities { get; }

    Task<IReadOnlyList<ProviderInfo>> GetAvailableProvidersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCountriesAsync(string providerExternalId, CancellationToken cancellationToken = default);
    Task<CatalogPage> ListTitlesAsync(CatalogListRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeavingEntry>> GetLeavingAsync(CatalogSourceRef source, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CatalogEntry>> GetChangedTitlesAsync(
        CatalogSourceRef source,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
