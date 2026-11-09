namespace Pickarr.Core.Catalog;

/// <summary>
/// Stable TMDB image CDN helpers for API responses (no network round-trip).
/// </summary>
public static class TmdbImageUrls
{
    public const string SecureBaseUrl = "https://image.tmdb.org/t/p/";

    public static string? Build(string? path, string size)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var relative = path.StartsWith('/') ? path : "/" + path;
        return $"{SecureBaseUrl.TrimEnd('/')}/{size}{relative}";
    }
}
