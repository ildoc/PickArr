namespace PickArr.Core.Metadata;

public sealed record TmdbGenre(int Id, string Name);

public sealed record TmdbKeyword(int Id, string Name);

/// <summary>Structured credit with TMDB person id (persist); distinct from <see cref="TmdbTitleDetails.Cast"/> name list.</summary>
public sealed record TmdbCredit(int PersonId, string Name, string Role, int Order);

public sealed record TmdbTitleDetails(
    int Id,
    string? Title,
    string? OriginalTitle,
    string? Overview,
    string? Tagline,
    int? Year,
    int? RuntimeMinutes,
    int? SeasonCount,
    int? EpisodeCount,
    double? VoteAverage,
    int? VoteCount,
    double? Popularity,
    string? OriginalLanguage,
    string? PosterPath,
    string? BackdropPath,
    string? ImdbId,
    int? TvdbId,
    IReadOnlyList<TmdbGenre> Genres,
    IReadOnlyList<TmdbKeyword> Keywords,
    bool IsMovie,
    IReadOnlyList<string>? Cast = null,
    IReadOnlyList<TmdbSeason>? Seasons = null,
    IReadOnlyList<TmdbCredit>? Credits = null,
    IReadOnlyList<string>? OriginCountries = null);

public sealed record TmdbSeason(
    int Number,
    string Name,
    int EpisodeCount,
    string? AirDate,
    string? Overview,
    IReadOnlyList<TmdbEpisode>? Episodes = null);

public sealed record TmdbEpisode(
    int Number,
    string Name,
    string? AirDate,
    string? Overview,
    int? RuntimeMinutes);

public sealed record TmdbFindResult(int TmdbId, string TmdbType);

public sealed record TmdbImageConfig(string SecureBaseUrl, DateTime FetchedAt);
