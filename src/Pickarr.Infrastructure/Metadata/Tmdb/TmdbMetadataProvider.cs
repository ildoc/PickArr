using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Metadata;

namespace Pickarr.Infrastructure.Metadata.Tmdb;

public sealed class TmdbMetadataProvider(
    HttpClient httpClient,
    ILogger<TmdbMetadataProvider> logger) : IMetadataProvider
{
    public const string ProviderType = "tmdb";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private TmdbImageConfig? _cachedConfig;
    private DateTime _configExpiresUtc;

    public string Type => ProviderType;

    public async Task<TmdbTitleDetails?> GetMovieAsync(
        int tmdbId,
        string language,
        CancellationToken cancellationToken = default)
    {
        var path = $"movie/{tmdbId}?language={Uri.EscapeDataString(language)}&append_to_response=external_ids,keywords,credits";
        var dto = await GetAsync<TmdbMovieDto>(path, cancellationToken);
        return dto is null ? null : MapMovie(dto);
    }

    public async Task<TmdbTitleDetails?> GetTvAsync(
        int tmdbId,
        string language,
        CancellationToken cancellationToken = default)
    {
        var path = $"tv/{tmdbId}?language={Uri.EscapeDataString(language)}&append_to_response=external_ids,keywords,credits";
        var dto = await GetAsync<TmdbTvDto>(path, cancellationToken);
        return dto is null ? null : MapTv(dto);
    }

    public async Task<IReadOnlyList<TmdbEpisode>> GetTvSeasonAsync(
        int tmdbId,
        int seasonNumber,
        string language,
        CancellationToken cancellationToken = default)
    {
        var path =
            $"tv/{tmdbId}/season/{seasonNumber}?language={Uri.EscapeDataString(language)}";
        var dto = await GetAsync<TmdbSeasonDetailsDto>(path, cancellationToken);
        return dto?.Episodes
            .Select(episode => new TmdbEpisode(
                episode.EpisodeNumber,
                string.IsNullOrWhiteSpace(episode.Name) ? $"Episode {episode.EpisodeNumber}" : episode.Name.Trim(),
                string.IsNullOrWhiteSpace(episode.AirDate) ? null : episode.AirDate,
                string.IsNullOrWhiteSpace(episode.Overview) ? null : episode.Overview,
                episode.Runtime is > 0 ? episode.Runtime : null))
            .OrderBy(episode => episode.Number)
            .ToList() ?? [];
    }

    public async Task<IReadOnlyList<TmdbRecommendationRef>> GetRecommendationsAsync(
        int tmdbId,
        bool isMovie,
        CancellationToken cancellationToken = default)
    {
        var kind = isMovie ? "movie" : "tv";
        var dto = await GetAsync<TmdbRecommendationsDto>($"{kind}/{tmdbId}/recommendations", cancellationToken);
        if (dto is null || dto.Results.Count == 0)
        {
            return [];
        }

        var list = new List<TmdbRecommendationRef>(dto.Results.Count);
        var rank = 1;
        foreach (var item in dto.Results)
        {
            if (item.Id <= 0)
            {
                continue;
            }

            list.Add(new TmdbRecommendationRef(item.Id, rank++));
        }

        return list;
    }

    public async Task<TmdbFindResult?> FindByImdbAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        var path = $"find/{Uri.EscapeDataString(imdbId)}?external_source=imdb_id";
        var dto = await GetAsync<TmdbFindDto>(path, cancellationToken);
        if (dto is null)
        {
            return null;
        }

        var movie = dto.MovieResults.FirstOrDefault();
        if (movie is not null)
        {
            return new TmdbFindResult(movie.Id, "movie");
        }

        var tv = dto.TvResults.FirstOrDefault();
        return tv is null ? null : new TmdbFindResult(tv.Id, "tv");
    }

    public async Task<IReadOnlyList<TmdbGenre>> GetMovieGenresAsync(
        string language,
        CancellationToken cancellationToken = default)
    {
        var dto = await GetAsync<TmdbGenreListDto>(
            $"genre/movie/list?language={Uri.EscapeDataString(language)}",
            cancellationToken);
        return dto?.Genres.Select(g => new TmdbGenre(g.Id, g.Name)).ToList() ?? [];
    }

    public async Task<IReadOnlyList<TmdbGenre>> GetTvGenresAsync(
        string language,
        CancellationToken cancellationToken = default)
    {
        var dto = await GetAsync<TmdbGenreListDto>(
            $"genre/tv/list?language={Uri.EscapeDataString(language)}",
            cancellationToken);
        return dto?.Genres.Select(g => new TmdbGenre(g.Id, g.Name)).ToList() ?? [];
    }

    public async Task<TmdbImageConfig> GetImageConfigAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedConfig is not null && DateTime.UtcNow < _configExpiresUtc)
        {
            return _cachedConfig;
        }

        var dto = await GetAsync<TmdbConfigurationDto>("configuration", cancellationToken)
            ?? throw new InvalidOperationException("TMDB configuration response was empty.");
        var baseUrl = dto.Images?.SecureBaseUrl
            ?? throw new InvalidOperationException("TMDB configuration missing secure_base_url.");
        _cachedConfig = new TmdbImageConfig(baseUrl, DateTime.UtcNow);
        _configExpiresUtc = DateTime.UtcNow.AddDays(7);
        return _cachedConfig;
    }

    private async Task<T?> GetAsync<T>(string relativePath, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(relativePath, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return default;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("TMDB call failed {Status}: {Body}", (int)response.StatusCode, body);
            throw new HttpRequestException($"TMDB request failed ({(int)response.StatusCode}).");
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private static TmdbTitleDetails MapMovie(TmdbMovieDto dto)
    {
        var keywords = dto.Keywords?.Keywords
            .Select(k => new TmdbKeyword(k.Id, k.Name))
            .ToList() ?? [];
        return new TmdbTitleDetails(
            dto.Id,
            dto.Title,
            dto.OriginalTitle,
            dto.Overview,
            dto.Tagline,
            ParseYear(dto.ReleaseDate),
            dto.Runtime,
            SeasonCount: null,
            EpisodeCount: null,
            dto.VoteAverage,
            dto.VoteCount,
            dto.Popularity,
            dto.OriginalLanguage,
            dto.PosterPath,
            dto.BackdropPath,
            dto.ImdbId ?? dto.ExternalIds?.ImdbId,
            dto.ExternalIds?.TvdbId,
            dto.Genres.Select(g => new TmdbGenre(g.Id, g.Name)).ToList(),
            keywords,
            IsMovie: true,
            CastNames(dto.Credits),
            Seasons: null,
            MapMovieCredits(dto.Credits),
            OriginCountries(dto.OriginCountry));
    }

    private static TmdbTitleDetails MapTv(TmdbTvDto dto)
    {
        var keywords = dto.Keywords?.Results
            .Select(k => new TmdbKeyword(k.Id, k.Name))
            .ToList() ?? [];
        var runtime = dto.EpisodeRunTime?.FirstOrDefault();
        return new TmdbTitleDetails(
            dto.Id,
            dto.Name,
            dto.OriginalName,
            dto.Overview,
            dto.Tagline,
            ParseYear(dto.FirstAirDate),
            runtime > 0 ? runtime : null,
            dto.NumberOfSeasons,
            dto.NumberOfEpisodes,
            dto.VoteAverage,
            dto.VoteCount,
            dto.Popularity,
            dto.OriginalLanguage,
            dto.PosterPath,
            dto.BackdropPath,
            dto.ExternalIds?.ImdbId,
            dto.ExternalIds?.TvdbId,
            dto.Genres.Select(g => new TmdbGenre(g.Id, g.Name)).ToList(),
            keywords,
            IsMovie: false,
            CastNames(dto.Credits),
            MapSeasons(dto.Seasons),
            MapTvCredits(dto.Credits, dto.CreatedBy),
            OriginCountries(dto.OriginCountry));
    }

    private static IReadOnlyList<TmdbSeason> MapSeasons(IEnumerable<TmdbSeasonDto> seasons) =>
        seasons
            .Select(season => new TmdbSeason(
                season.SeasonNumber,
                string.IsNullOrWhiteSpace(season.Name)
                    ? season.SeasonNumber == 0 ? "Specials" : $"Season {season.SeasonNumber}"
                    : season.Name.Trim(),
                Math.Max(0, season.EpisodeCount),
                string.IsNullOrWhiteSpace(season.AirDate) ? null : season.AirDate,
                string.IsNullOrWhiteSpace(season.Overview) ? null : season.Overview))
            .ToList();

    private static IReadOnlyList<string> CastNames(TmdbCreditsDto? credits) =>
        credits?.Cast
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .OrderBy(c => c.Order)
            .Select(c => c.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList() ?? [];

    private static IReadOnlyList<TmdbCredit> MapMovieCredits(TmdbCreditsDto? credits)
    {
        var list = new List<TmdbCredit>();
        if (credits is null)
        {
            return list;
        }

        foreach (var cast in credits.Cast
                     .Where(c => c.Id > 0 && !string.IsNullOrWhiteSpace(c.Name))
                     .OrderBy(c => c.Order)
                     .Take(5))
        {
            list.Add(new TmdbCredit(cast.Id, cast.Name.Trim(), CreditRoles.Cast, cast.Order));
        }

        foreach (var crew in credits.Crew
                     .Where(c =>
                         c.Id > 0
                         && !string.IsNullOrWhiteSpace(c.Name)
                         && string.Equals(c.Job, "Director", StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(new TmdbCredit(crew.Id, crew.Name.Trim(), CreditRoles.Director, Order: 0));
        }

        return list;
    }

    private static IReadOnlyList<TmdbCredit> MapTvCredits(
        TmdbCreditsDto? credits,
        IEnumerable<TmdbCreatedByDto> createdBy)
    {
        var list = new List<TmdbCredit>();
        if (credits is not null)
        {
            foreach (var cast in credits.Cast
                         .Where(c => c.Id > 0 && !string.IsNullOrWhiteSpace(c.Name))
                         .OrderBy(c => c.Order)
                         .Take(5))
            {
                list.Add(new TmdbCredit(cast.Id, cast.Name.Trim(), CreditRoles.Cast, cast.Order));
            }
        }

        var order = 0;
        foreach (var creator in createdBy.Where(c => c.Id > 0 && !string.IsNullOrWhiteSpace(c.Name)))
        {
            list.Add(new TmdbCredit(creator.Id, creator.Name.Trim(), CreditRoles.Creator, order++));
        }

        return list;
    }

    private static IReadOnlyList<string>? OriginCountries(IReadOnlyList<string> countries)
    {
        var cleaned = countries
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return cleaned.Count == 0 ? null : cleaned;
    }

    private static int? ParseYear(string? date)
    {
        if (string.IsNullOrWhiteSpace(date) || date.Length < 4)
        {
            return null;
        }

        return int.TryParse(date.AsSpan(0, 4), out var year) ? year : null;
    }
}
