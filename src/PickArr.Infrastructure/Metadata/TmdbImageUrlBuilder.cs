using Pickarr.Core.Abstractions;

namespace Pickarr.Infrastructure.Metadata;

public sealed class TmdbImageUrlBuilder(IMetadataProvider metadataProvider) : ITmdbImageUrlBuilder
{
    public async Task<string?> BuildPosterUrlAsync(
        string? posterPath,
        string size = "w342",
        CancellationToken cancellationToken = default) =>
        await BuildAsync(posterPath, size, cancellationToken);

    public async Task<string?> BuildBackdropUrlAsync(
        string? backdropPath,
        string size = "w780",
        CancellationToken cancellationToken = default) =>
        await BuildAsync(backdropPath, size, cancellationToken);

    private async Task<string?> BuildAsync(string? path, string size, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var config = await metadataProvider.GetImageConfigAsync(cancellationToken);
        var baseUrl = config.SecureBaseUrl.TrimEnd('/');
        var relative = path.StartsWith('/') ? path : "/" + path;
        return $"{baseUrl}/{size}{relative}";
    }
}
