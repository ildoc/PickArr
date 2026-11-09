using Pickarr.Core.Abstractions;
using Pickarr.Core.Catalog;

namespace Pickarr.Infrastructure.Catalog;

public sealed class ProviderResolver(IEnumerable<ICatalogProvider> providers) : IProviderResolver
{
    private readonly Dictionary<string, ICatalogProvider> _byType =
        providers.ToDictionary(p => p.Type, StringComparer.OrdinalIgnoreCase);

    public ICatalogProvider Resolve(string providerType)
    {
        if (_byType.TryGetValue(providerType, out var provider))
        {
            return provider;
        }

        throw new CatalogProviderNotSupportedException(providerType);
    }
}
