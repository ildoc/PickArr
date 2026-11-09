namespace Pickarr.Core.Abstractions;

public interface IProviderResolver
{
    ICatalogProvider Resolve(string providerType);
}
