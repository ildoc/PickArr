namespace Pickarr.Infrastructure.Jobs;

public sealed class CatalogBootstrapOptions
{
    public const string SectionName = "Pickarr:Catalog";

    /// <summary>
    /// When true, the first time a Watchmode API key is available the app enables the seeded
    /// country catalogs and queues their first import.
    /// </summary>
    public bool AutoInitialImport { get; set; } = true;
}
