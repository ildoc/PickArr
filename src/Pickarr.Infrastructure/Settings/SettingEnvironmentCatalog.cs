using System.Text;
using Pickarr.Core.Settings;

namespace Pickarr.Infrastructure.Settings;

internal enum SettingValueKind
{
    String,
    Url,
    Int32,
    Double,
    Boolean,
    Defaults,
}

/// <summary>
/// Settings that can be declared with flat names (<c>WATCHMODE_API_KEY</c>, <c>SONARR_URL</c>)
/// or the legacy <c>Pickarr__Settings__…</c> form.
/// <c>catalog.initialImportStarted</c> is runtime state and is not included.
/// </summary>
internal static class SettingEnvironmentCatalog
{
    public const string SectionName = "Pickarr:Settings";

    private static readonly Dictionary<string, SettingValueKind> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        [SettingsKeys.WatchmodeApiKey] = SettingValueKind.String,
        [SettingsKeys.WatchmodePlan] = SettingValueKind.String,
        [SettingsKeys.WatchmodeQuota] = SettingValueKind.Int32,
        [SettingsKeys.WatchmodeReservePercent] = SettingValueKind.Int32,
        [SettingsKeys.WatchmodeMaxCreditsPerRun] = SettingValueKind.Int32,
        [SettingsKeys.WatchmodeBillingDayOfMonth] = SettingValueKind.Int32,
        [SettingsKeys.CatalogMaxRemovalRatio] = SettingValueKind.Double,
        [SettingsKeys.CatalogRemovalConfirmations] = SettingValueKind.Int32,
        [SettingsKeys.CatalogNewWindowDays] = SettingValueKind.Int32,
        [SettingsKeys.CatalogLeavingWindowDays] = SettingValueKind.Int32,
        [SettingsKeys.CatalogRemovedWindowDays] = SettingValueKind.Int32,
        [SettingsKeys.CatalogStaleWarningDays] = SettingValueKind.Int32,
        [SettingsKeys.TmdbApiKey] = SettingValueKind.String,
        [SettingsKeys.OmdbApiKey] = SettingValueKind.String,
        [SettingsKeys.SonarrUrl] = SettingValueKind.Url,
        [SettingsKeys.SonarrApiKey] = SettingValueKind.String,
        [SettingsKeys.SonarrDefaults] = SettingValueKind.Defaults,
        [SettingsKeys.SonarrSyncIntervalMinutes] = SettingValueKind.Int32,
        [SettingsKeys.RadarrUrl] = SettingValueKind.Url,
        [SettingsKeys.RadarrApiKey] = SettingValueKind.String,
        [SettingsKeys.RadarrDefaults] = SettingValueKind.Defaults,
        [SettingsKeys.RadarrSyncIntervalMinutes] = SettingValueKind.Int32,
        [SettingsKeys.WebhooksToken] = SettingValueKind.String,
        [SettingsKeys.UiLanguage] = SettingValueKind.String,
        [SettingsKeys.RecoEnabled] = SettingValueKind.Boolean,
        [SettingsKeys.RecoUseImplicit] = SettingValueKind.Boolean,
        [SettingsKeys.RecoWeightLove] = SettingValueKind.Double,
        [SettingsKeys.RecoWeightLike] = SettingValueKind.Double,
        [SettingsKeys.RecoWeightDislike] = SettingValueKind.Double,
        [SettingsKeys.RecoWeightNotInterested] = SettingValueKind.Double,
        [SettingsKeys.RecoWeightFavorite] = SettingValueKind.Double,
        [SettingsKeys.RecoWeightArrAdd] = SettingValueKind.Double,
        [SettingsKeys.RecoWeightDownloaded] = SettingValueKind.Double,
        [SettingsKeys.RecoWeightWatched] = SettingValueKind.Double,
        [SettingsKeys.RecoN0] = SettingValueKind.Int32,
        [SettingsKeys.RecoTmdbAmplifierMaxShare] = SettingValueKind.Double,
        [SettingsKeys.RecoExcludeInLibrary] = SettingValueKind.Boolean,
        [SettingsKeys.RecoEmbeddingsEnabled] = SettingValueKind.Boolean,
        [SettingsKeys.RecoEmbeddingsModelId] = SettingValueKind.String,
        [SettingsKeys.RecoEmbeddingsThreads] = SettingValueKind.Int32,
        [SettingsKeys.RecoEmbeddingsBatchSize] = SettingValueKind.Int32,
        [SettingsKeys.RecoEmbeddingsMinCoverage] = SettingValueKind.Double,
        [SettingsKeys.RecoEmbeddingsThrottle] = SettingValueKind.Boolean,
        [SettingsKeys.RecoMmrLambda] = SettingValueKind.Double,
        [SettingsKeys.RecoExploreRate] = SettingValueKind.Double,
        [SettingsKeys.RecoMlMinRatings] = SettingValueKind.Int32,
        [SettingsKeys.RecoEventsEnabled] = SettingValueKind.Boolean,
    };

    public static IEnumerable<string> CanonicalKeys => Keys.Keys;

    public static IEnumerable<(string Canonical, SettingValueKind Kind)> Entries =>
        Keys.Select(pair => (pair.Key, pair.Value));

    public static bool TryGet(string key, out string canonical, out SettingValueKind kind)
    {
        foreach (var pair in Keys)
        {
            if (pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                canonical = pair.Key;
                kind = pair.Value;
                return true;
            }
        }

        canonical = "";
        kind = default;
        return false;
    }

    public static bool TryMatchDefaultsField(string dotted, out string defaultsKey, out string field)
    {
        foreach (var pair in Keys)
        {
            if (pair.Value != SettingValueKind.Defaults)
            {
                continue;
            }

            var prefix = pair.Key + ".";
            if (!dotted.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rest = dotted[prefix.Length..];
            if (rest.Length == 0 || rest.Contains('.'))
            {
                continue;
            }

            defaultsKey = pair.Key;
            field = rest;
            return true;
        }

        defaultsKey = "";
        field = "";
        return false;
    }

    /// <summary>
    /// <c>watchmode.apiKey</c> → <c>WATCHMODE_API_KEY</c>;
    /// <c>sonarr.defaults.qualityProfileId</c> → <c>SONARR_DEFAULTS_QUALITY_PROFILE_ID</c>.
    /// </summary>
    public static string ToEnvironmentVariableName(string dottedKey)
    {
        var parts = dottedKey.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var builder = new StringBuilder();
        foreach (var part in parts)
        {
            if (builder.Length > 0)
            {
                builder.Append('_');
            }

            AppendCamelAsSnake(builder, part);
        }

        return builder.ToString();
    }

    private static void AppendCamelAsSnake(StringBuilder builder, string part)
    {
        for (var i = 0; i < part.Length; i++)
        {
            var c = part[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(part[i - 1]))
            {
                builder.Append('_');
            }
            else if (i > 0 && char.IsUpper(c) && char.IsUpper(part[i - 1])
                     && i + 1 < part.Length && char.IsLower(part[i + 1]))
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(c));
        }
    }
}
