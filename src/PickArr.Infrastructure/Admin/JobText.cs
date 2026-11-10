namespace Pickarr.Infrastructure.Admin;

internal static class JobText
{
    public static string CountryName(string code) => code.ToUpperInvariant() switch
    {
        "IT" => "Italy",
        "US" => "United States",
        _ => code,
    };

    public static string IncrementalName(string provider, string code) =>
        $"Import recent titles ({provider} {code})";

    public static string IncrementalDescription(string provider, string code) =>
        $"Downloads the newest pages of the {provider} catalog for {CountryName(code)}.";

    public static string FullName(string provider, string code) =>
        $"Full catalog import ({provider} {code})";

    public static string FullDescription(string provider, string code) =>
        $"Walks the whole {provider} catalog for {CountryName(code)} and records titles that are no longer available.";

    public static string LeavingName(string code) => $"Titles leaving soon ({code})";

    public static string LeavingDescription(string code) =>
        $"Refreshes the short preview of titles about to leave streaming catalogs in {CountryName(code)} (one Watchmode call for the country).";

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

    public const string RecoFeatureName = "Recommendation features";

    public const string RecoFeatureDescription =
        "Backfills keywords, credits, and English metadata that recommendations need for Ready titles.";

    public const string RecoEmbeddingName = "Title embeddings";

    public const string RecoEmbeddingDescription =
        "Computes or refreshes ONNX embeddings for titles that are missing them or whose text changed.";

    public const string RecoBuildName = "Recommendations rebuild";

    public const string RecoBuildDescription =
        "Rebuilds taste profiles and recommendation scores for every profile and publishes a new version.";

    public const string RecoEvalName = "Recommendation evaluation";

    public const string RecoEvalDescription =
        "Runs the offline evaluation report for the current recommendation pipeline.";

    public const string RecoModelDownloadName = "Recommendation model";

    public const string RecoModelDownloadDescription =
        "Downloads the ONNX embedding model, verifies its hash, and runs the self-test.";
}
