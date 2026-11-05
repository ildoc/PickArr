using PickArr.Core.Domain;

namespace PickArr.Core.Catalog;

public static class CatalogMediaMapping
{
    public static string ToMediaType(CatalogEntryType type) => type switch
    {
        CatalogEntryType.Movie => MediaTypes.Movie,
        CatalogEntryType.TvSeries or CatalogEntryType.TvMiniseries or CatalogEntryType.TvSpecial => MediaTypes.Series,
        _ => MediaTypes.Series,
    };

    public static string ToCategory(CatalogEntryType type) => ToMediaType(type) switch
    {
        MediaTypes.Movie => TitleCategories.Movie,
        _ => TitleCategories.Series,
    };

    public static string InitialMetadataStatus(int? tmdbId) =>
        tmdbId is > 0 ? MetadataStatuses.Pending : MetadataStatuses.Unmapped;
}
