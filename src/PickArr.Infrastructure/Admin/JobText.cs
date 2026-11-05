namespace PickArr.Infrastructure.Admin;

internal static class JobText
{
    public static string CountryName(string code) => code.ToUpperInvariant() switch
    {
        "IT" => "Italy",
        "US" => "United States",
        _ => code,
    };

    public static string IncrementalName(string code) => $"Import recent titles ({code})";

    public static string IncrementalDescription(string code) =>
        $"Downloads the newest pages of the Netflix catalog for {CountryName(code)}.";

    public static string FullName(string code) => $"Full catalog import ({code})";

    public static string FullDescription(string code) =>
        $"Walks the whole Netflix catalog for {CountryName(code)} and records titles that are no longer available.";

    public static string LeavingName(string code) => $"Titles leaving soon ({code})";

    public static string LeavingDescription(string code) =>
        $"Refreshes the short preview of titles about to leave Netflix in {CountryName(code)}.";

    public const string ResumeName = "Resume paused imports";

    public const string ResumeDescription =
        "Re-queues catalog imports that stopped because the Watchmode credit budget ran out. It does not download titles by itself.";

    public const string BootstrapName = "Initial catalog import";

    public const string BootstrapDescription =
        "Queues the first import for every catalog that is turned on and has not been imported yet. Runs automatically the first time a Watchmode API key is saved.";

    public const string EnrichmentName = "Metadata enrichment";

    public const string EnrichmentDescription =
        "Fills in posters, overviews, cast, genres, and external ids from TMDB for titles that do not have them yet.";

    public const string RefreshName = "Metadata refresh";

    public const string RefreshDescription =
        "Updates TMDB metadata that is already stored and has gone stale.";

    public const string SonarrName = "Sonarr library sync";

    public const string SonarrDescription =
        "Reads the Sonarr library and matches it to catalog titles.";

    public const string RadarrName = "Radarr library sync";

    public const string RadarrDescription =
        "Reads the Radarr library and matches it to catalog titles.";
}
