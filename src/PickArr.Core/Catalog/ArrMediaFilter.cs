using Pickarr.Core.Domain;

namespace Pickarr.Core.Catalog;

/// <summary>
/// Arr instance configuration helpers. Catalog browse always includes Series and Movie;
/// Sonarr/Radarr gate only add/status/delete actions per media type.
/// </summary>
public static class ArrMediaFilter
{
    public static bool IsConfigured(string? url, bool apiKeySet) =>
        !string.IsNullOrWhiteSpace(url) && apiKeySet;

    /// <summary>
    /// Media types shown in catalog browse. Always Series and Movie; Arr config does not filter the grid.
    /// </summary>
    public static IReadOnlyList<string> AllowedMediaTypes() =>
        [MediaTypes.Series, MediaTypes.Movie];
}
