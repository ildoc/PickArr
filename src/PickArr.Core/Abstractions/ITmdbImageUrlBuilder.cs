namespace PickArr.Core.Abstractions;

public interface ITmdbImageUrlBuilder
{
    Task<string?> BuildPosterUrlAsync(string? posterPath, string size = "w342", CancellationToken cancellationToken = default);
    Task<string?> BuildBackdropUrlAsync(string? backdropPath, string size = "w780", CancellationToken cancellationToken = default);
}
