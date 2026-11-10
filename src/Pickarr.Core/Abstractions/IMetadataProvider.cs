using Pickarr.Core.Metadata;

namespace Pickarr.Core.Abstractions;

public interface IMetadataProvider
{
    string Type { get; }

    Task<TmdbTitleDetails?> GetMovieAsync(int tmdbId, string language, CancellationToken cancellationToken = default);
    Task<TmdbTitleDetails?> GetTvAsync(int tmdbId, string language, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TmdbEpisode>> GetTvSeasonAsync(
        int tmdbId,
        int seasonNumber,
        string language,
        CancellationToken cancellationToken = default);
    /// <summary>TMDB item–item recommendations (page 1). Empty when the resource is missing.</summary>
    Task<IReadOnlyList<TmdbRecommendationRef>> GetRecommendationsAsync(
        int tmdbId,
        bool isMovie,
        CancellationToken cancellationToken = default);
    Task<TmdbFindResult?> FindByImdbAsync(string imdbId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TmdbGenre>> GetMovieGenresAsync(string language, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TmdbGenre>> GetTvGenresAsync(string language, CancellationToken cancellationToken = default);
    Task<TmdbImageConfig> GetImageConfigAsync(CancellationToken cancellationToken = default);
}
