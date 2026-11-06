using System.Text.Json.Serialization;

namespace PickArr.Infrastructure.Metadata.Tmdb;

internal sealed class TmdbMovieDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("original_title")] public string? OriginalTitle { get; set; }
    [JsonPropertyName("overview")] public string? Overview { get; set; }
    [JsonPropertyName("tagline")] public string? Tagline { get; set; }
    [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }
    [JsonPropertyName("runtime")] public int? Runtime { get; set; }
    [JsonPropertyName("vote_average")] public double? VoteAverage { get; set; }
    [JsonPropertyName("vote_count")] public int? VoteCount { get; set; }
    [JsonPropertyName("popularity")] public double? Popularity { get; set; }
    [JsonPropertyName("original_language")] public string? OriginalLanguage { get; set; }
    [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
    [JsonPropertyName("backdrop_path")] public string? BackdropPath { get; set; }
    [JsonPropertyName("imdb_id")] public string? ImdbId { get; set; }
    [JsonPropertyName("origin_country")] public List<string> OriginCountry { get; set; } = [];
    [JsonPropertyName("genres")] public List<TmdbGenreDto> Genres { get; set; } = [];
    [JsonPropertyName("external_ids")] public TmdbExternalIdsDto? ExternalIds { get; set; }
    [JsonPropertyName("keywords")] public TmdbMovieKeywordsDto? Keywords { get; set; }
    [JsonPropertyName("credits")] public TmdbCreditsDto? Credits { get; set; }
}

internal sealed class TmdbTvDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("original_name")] public string? OriginalName { get; set; }
    [JsonPropertyName("overview")] public string? Overview { get; set; }
    [JsonPropertyName("tagline")] public string? Tagline { get; set; }
    [JsonPropertyName("first_air_date")] public string? FirstAirDate { get; set; }
    [JsonPropertyName("number_of_seasons")] public int? NumberOfSeasons { get; set; }
    [JsonPropertyName("number_of_episodes")] public int? NumberOfEpisodes { get; set; }
    [JsonPropertyName("episode_run_time")] public List<int>? EpisodeRunTime { get; set; }
    [JsonPropertyName("vote_average")] public double? VoteAverage { get; set; }
    [JsonPropertyName("vote_count")] public int? VoteCount { get; set; }
    [JsonPropertyName("popularity")] public double? Popularity { get; set; }
    [JsonPropertyName("original_language")] public string? OriginalLanguage { get; set; }
    [JsonPropertyName("poster_path")] public string? PosterPath { get; set; }
    [JsonPropertyName("backdrop_path")] public string? BackdropPath { get; set; }
    [JsonPropertyName("origin_country")] public List<string> OriginCountry { get; set; } = [];
    [JsonPropertyName("created_by")] public List<TmdbCreatedByDto> CreatedBy { get; set; } = [];
    [JsonPropertyName("genres")] public List<TmdbGenreDto> Genres { get; set; } = [];
    [JsonPropertyName("external_ids")] public TmdbExternalIdsDto? ExternalIds { get; set; }
    [JsonPropertyName("keywords")] public TmdbTvKeywordsDto? Keywords { get; set; }
    [JsonPropertyName("credits")] public TmdbCreditsDto? Credits { get; set; }
    [JsonPropertyName("seasons")] public List<TmdbSeasonDto> Seasons { get; set; } = [];
}

internal sealed class TmdbSeasonDto
{
    [JsonPropertyName("season_number")] public int SeasonNumber { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("episode_count")] public int EpisodeCount { get; set; }
    [JsonPropertyName("air_date")] public string? AirDate { get; set; }
    [JsonPropertyName("overview")] public string? Overview { get; set; }
}

internal sealed class TmdbSeasonDetailsDto
{
    [JsonPropertyName("episodes")] public List<TmdbEpisodeDto> Episodes { get; set; } = [];
}

internal sealed class TmdbEpisodeDto
{
    [JsonPropertyName("episode_number")] public int EpisodeNumber { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("air_date")] public string? AirDate { get; set; }
    [JsonPropertyName("overview")] public string? Overview { get; set; }
    [JsonPropertyName("runtime")] public int? Runtime { get; set; }
}

internal sealed class TmdbGenreDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

internal sealed class TmdbExternalIdsDto
{
    [JsonPropertyName("imdb_id")] public string? ImdbId { get; set; }
    [JsonPropertyName("tvdb_id")] public int? TvdbId { get; set; }
}

internal sealed class TmdbMovieKeywordsDto
{
    [JsonPropertyName("keywords")] public List<TmdbKeywordDto> Keywords { get; set; } = [];
}

internal sealed class TmdbTvKeywordsDto
{
    [JsonPropertyName("results")] public List<TmdbKeywordDto> Results { get; set; } = [];
}

internal sealed class TmdbKeywordDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

internal sealed class TmdbCreditsDto
{
    [JsonPropertyName("cast")] public List<TmdbCastDto> Cast { get; set; } = [];
    [JsonPropertyName("crew")] public List<TmdbCrewDto> Crew { get; set; } = [];
}

internal sealed class TmdbCastDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("order")] public int Order { get; set; }
}

internal sealed class TmdbCrewDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("job")] public string? Job { get; set; }
    [JsonPropertyName("department")] public string? Department { get; set; }
}

internal sealed class TmdbCreatedByDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

internal sealed class TmdbFindDto
{
    [JsonPropertyName("movie_results")] public List<TmdbFindItemDto> MovieResults { get; set; } = [];
    [JsonPropertyName("tv_results")] public List<TmdbFindItemDto> TvResults { get; set; } = [];
}

internal sealed class TmdbFindItemDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("media_type")] public string? MediaType { get; set; }
}

internal sealed class TmdbGenreListDto
{
    [JsonPropertyName("genres")] public List<TmdbGenreDto> Genres { get; set; } = [];
}

internal sealed class TmdbConfigurationDto
{
    [JsonPropertyName("images")] public TmdbImagesDto? Images { get; set; }
}

internal sealed class TmdbImagesDto
{
    [JsonPropertyName("secure_base_url")] public string? SecureBaseUrl { get; set; }
}
