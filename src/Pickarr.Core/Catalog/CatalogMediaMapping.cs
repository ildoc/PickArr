using Pickarr.Core.Domain;

namespace Pickarr.Core.Catalog;

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

    /// <summary>
    /// Pending when Watchmode already has a TMDB id, or when an IMDb id can drive TMDB <c>/find</c>.
    /// Unmapped only when neither id is usable at upsert time.
    /// </summary>
    public static string InitialMetadataStatus(int? tmdbId, string? imdbId = null) =>
        tmdbId is > 0 || !string.IsNullOrWhiteSpace(imdbId)
            ? MetadataStatuses.Pending
            : MetadataStatuses.Unmapped;
}
