namespace Pickarr.Core.Catalog;

public enum CatalogEntryType
{
    Movie,
    TvSeries,
    TvMiniseries,
    TvSpecial,
}

public enum CatalogSort
{
    TitleAsc,
    TitleDesc,
    PopularityDesc,
    RelevanceDesc,
    ReleaseDateDesc,
}

public sealed record CatalogProviderCapabilities(
    bool SupportsChangeFeed,
    bool SupportsLeavingDates,
    int MaxPageSize);

public sealed record ProviderInfo(
    string ExternalId,
    string Name,
    string? Type,
    IReadOnlyList<string> Regions);

public sealed record CatalogSourceRef(string ProviderExternalId, string CountryCode);

public sealed record CatalogListRequest(
    string ProviderExternalId,
    string CountryCode,
    IReadOnlyList<CatalogEntryType> Types,
    CatalogSort Sort,
    int Page,
    int PageSize);

public sealed record CatalogEntry(
    string ExternalId,
    string Title,
    int? Year,
    CatalogEntryType Type,
    int? TmdbId,
    string? TmdbType,
    string? ImdbId);

public sealed record CatalogPage(
    IReadOnlyList<CatalogEntry> Entries,
    int Page,
    int TotalPages,
    int CreditsUsed,
    int TotalResults = 0);

public sealed record LeavingEntry(
    string ExternalId,
    string Title,
    CatalogEntryType Type,
    string CountryCode,
    DateOnly LeavingDate,
    int? Season,
    string? ProviderExternalId);
