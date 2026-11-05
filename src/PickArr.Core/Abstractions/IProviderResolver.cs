namespace PickArr.Core.Abstractions;

public interface IProviderResolver
{
    ICatalogProvider Resolve(string providerType);
}
