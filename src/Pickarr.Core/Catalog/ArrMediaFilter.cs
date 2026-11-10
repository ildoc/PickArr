using Pickarr.Core.Domain;

namespace Pickarr.Core.Catalog;

/// <summary>
/// V-08 / D-011: catalog media types allowed by configured Arr instances.
/// Sonarr → Series; Radarr → Movie; neither → empty. Stand-up follows MediaType.
/// </summary>
public static class ArrMediaFilter
{
    public const string EmptyReasonArrNotConfigured = "arrNotConfigured";

    public static bool IsConfigured(string? url, bool apiKeySet) =>
        !string.IsNullOrWhiteSpace(url) && apiKeySet;

    public static IReadOnlyList<string> AllowedMediaTypes(bool sonarrConfigured, bool radarrConfigured)
    {
        var allowed = new List<string>(2);
        if (sonarrConfigured)
        {
            allowed.Add(MediaTypes.Series);
        }

        if (radarrConfigured)
        {
            allowed.Add(MediaTypes.Movie);
        }

        return allowed;
    }
}
