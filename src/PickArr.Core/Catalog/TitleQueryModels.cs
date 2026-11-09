namespace PickArr.Core.Catalog;

public sealed record TitleQueryRequest(
    string? Q = null,
    string View = "all",
    IReadOnlyList<string>? Types = null,
    IReadOnlyList<int>? Genres = null,
    int? YearFrom = null,
    int? YearTo = null,
    IReadOnlyList<string>? ArrStates = null,
    bool? Favorite = null,
    /// <summary>API rating filter: <c>like</c> or <c>dislike</c>.</summary>
    string? RatingValue = null,
    bool IncludeRemoved = false,
    double? MinRating = null,
    string Sort = "added",
    string Dir = "desc",
    int Page = 1,
    int PageSize = 48);

public sealed record PagedTitlesResult(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<TitleListItemDto> Items,
    string? EmptyReason = null,
    ArrFilterInfoDto? ArrFilter = null);

public sealed record ArrFilterInfoDto(
    bool SonarrConfigured,
    bool RadarrConfigured,
    IReadOnlyList<string> AllowedMediaTypes);

public sealed record TitleListItemDto(
    int Id,
    string Name,
    string? OriginalTitle,
    int? Year,
    string Category,
    string? PosterUrl,
    IReadOnlyList<GenreRefDto> Genres,
    double? RatingTmdb,
    double? RatingImdb,
    bool IsFavorite,
    bool IsWatched,
    string? Rating,
    AvailabilitySummaryDto? Availability,
    ArrSummaryDto? Arr,
    IReadOnlyList<CatalogPlaceDto> Places);

public sealed record GenreRefDto(int Id, string Name);

public sealed record AvailabilitySummaryDto(
    DateTime? AddedAt,
    DateTime? RemovedAt,
    DateOnly? LeavingDate,
    bool IsNew);

public sealed record ArrSummaryDto(
    string Kind,
    string State,
    bool? Monitored = null,
    double? Progress = null);

public sealed record CatalogSourceDto(
    int Id,
    string ProviderKey,
    string ProviderName,
    string CountryCode,
    string MetadataLanguage,
    bool BaselineCompleted,
    DateTime? LastFullSyncAt,
    bool DataStale);

public sealed record GenreCountDto(int Id, string Name, int Count);

public sealed record CatalogTypeCountDto(string Type, int Count);

public sealed record CatalogPlaceDto(string ProviderName, string CountryCode, DateTime? RemovedAt = null);

public sealed record TitleDetailDto(
    int Id,
    string Name,
    string? OriginalTitle,
    int? Year,
    string Category,
    string? Overview,
    int? RuntimeMinutes,
    int? SeasonCount,
    int? EpisodeCount,
    IReadOnlyList<GenreRefDto> Genres,
    string? PosterUrl,
    string? BackdropUrl,
    TitleRatingsDto Ratings,
    TitleLinksDto Links,
    bool IsFavorite,
    bool IsWatched,
    string? Rating,
    string MetadataStatus,
    IReadOnlyList<TitleAvailabilityDto> Availability,
    ArrDetailDto Arr,
    IReadOnlyList<string> Cast);

public sealed record TitleRatingsDto(double? Tmdb, int? TmdbVotes, double? Imdb);

public sealed record TitleLinksDto(string? Tmdb, string? Imdb, string? Tvdb);

public sealed record TitleAvailabilityDto(
    int SourceId,
    string CountryCode,
    DateTime? AddedAt,
    DateTime? RemovedAt,
    DateOnly? LeavingDate);

public sealed record ArrDetailDto(
    string? Kind,
    int? ArrId,
    string State,
    bool? Monitored,
    double? Progress,
    long? SizeOnDisk,
    string? Path,
    int? FileCount,
    DateTime? SyncedAt,
    IReadOnlyList<ArrSeasonDto>? Seasons,
    bool CanAdd,
    bool CanDelete,
    string? AddBlockedReason,
    string? ExternalUrl);

public sealed record ArrSeasonDto(
    int Number,
    bool Monitored,
    string State,
    int EpisodeFileCount,
    int EpisodeCount,
    int TotalEpisodeCount);
