namespace PickArr.Core.Catalog;

public sealed class CatalogProviderNotSupportedException : Exception
{
    public CatalogProviderNotSupportedException(string providerType)
        : base($"No catalog provider registered for type '{providerType}'.")
    {
        ProviderType = providerType;
    }

    public string ProviderType { get; }
}
