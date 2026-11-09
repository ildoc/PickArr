using Pickarr.Core.Catalog;

namespace Pickarr.Infrastructure.Catalog.Watchmode;

internal static class WatchmodeMapping
{
    public static string ToWatchmodeType(CatalogEntryType type) => type switch
    {
        CatalogEntryType.Movie => "movie",
        CatalogEntryType.TvSeries => "tv_series",
        CatalogEntryType.TvMiniseries => "tv_miniseries",
        CatalogEntryType.TvSpecial => "tv_special",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static string ToWatchmodeSort(CatalogSort sort) => sort switch
    {
        CatalogSort.TitleAsc => "title_asc",
        CatalogSort.TitleDesc => "title_desc",
        CatalogSort.PopularityDesc => "popularity_desc",
        CatalogSort.RelevanceDesc => "relevance_desc",
        CatalogSort.ReleaseDateDesc => "release_date_desc",
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null),
    };

    public static CatalogEntryType ToCatalogEntryType(string? watchmodeType, string? tmdbType)
    {
        switch (watchmodeType)
        {
            case "movie":
                return CatalogEntryType.Movie;
            case "tv_series":
                return CatalogEntryType.TvSeries;
            case "tv_miniseries":
                return CatalogEntryType.TvMiniseries;
            case "tv_special":
                return string.Equals(tmdbType, "movie", StringComparison.OrdinalIgnoreCase)
                    ? CatalogEntryType.Movie
                    : CatalogEntryType.TvSpecial;
            default:
                if (string.Equals(tmdbType, "movie", StringComparison.OrdinalIgnoreCase))
                {
                    return CatalogEntryType.Movie;
                }

                return CatalogEntryType.TvSeries;
        }
    }

    public static CatalogEntry ToCatalogEntry(WatchmodeTitleItem item) =>
        new(
            ExternalId: item.Id.ToString(),
            Title: item.Title,
            Year: item.Year,
            Type: ToCatalogEntryType(item.Type, item.TmdbType),
            TmdbId: item.TmdbId,
            TmdbType: item.TmdbType,
            ImdbId: item.ImdbId);

    public static LeavingEntry ToLeavingEntry(WatchmodeLeavingItem item) =>
        new(
            ExternalId: item.Id.ToString(),
            Title: item.Title,
            Type: ToCatalogEntryType(item.TitleType, tmdbType: null),
            CountryCode: item.Region,
            LeavingDate: DateOnly.Parse(item.LeavingDate),
            Season: item.Season,
            ProviderExternalId: item.ProviderId?.ToString());
}
