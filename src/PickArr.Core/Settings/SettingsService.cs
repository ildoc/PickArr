using System.Text.Json;
using PickArr.Core.Abstractions;

namespace PickArr.Core.Settings;

public sealed class SettingsService(ISettingsStore store)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SettingsResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        return new SettingsResponse
        {
            Watchmode = new WatchmodeSettingsDto
            {
                ApiKey = new SecretStatus(await store.IsSetAsync(SettingsKeys.WatchmodeApiKey, cancellationToken)),
                Plan = await GetOrDefaultAsync(SettingsKeys.WatchmodePlan, "free", cancellationToken),
                Quota = await GetOrDefaultAsync(SettingsKeys.WatchmodeQuota, 2500, cancellationToken),
                ReservePercent = await GetOrDefaultAsync(SettingsKeys.WatchmodeReservePercent, 10, cancellationToken),
                MaxCreditsPerRun = await GetOrDefaultAsync(SettingsKeys.WatchmodeMaxCreditsPerRun, 80, cancellationToken),
            },
            Tmdb = new TmdbSettingsDto
            {
                Token = new SecretStatus(await store.IsSetAsync(SettingsKeys.TmdbToken, cancellationToken)),
            },
            Omdb = new OmdbSettingsDto
            {
                ApiKey = new SecretStatus(await store.IsSetAsync(SettingsKeys.OmdbApiKey, cancellationToken)),
            },
            Sonarr = new ArrInstanceSettingsDto
            {
                Url = await GetNullableAsync(SettingsKeys.SonarrUrl, cancellationToken),
                ApiKey = new SecretStatus(await store.IsSetAsync(SettingsKeys.SonarrApiKey, cancellationToken)),
                SyncIntervalMinutes = await GetOrDefaultAsync(SettingsKeys.SonarrSyncIntervalMinutes, 15, cancellationToken),
                Defaults = await GetDefaultsAsync(SettingsKeys.SonarrDefaults, cancellationToken),
            },
            Radarr = new ArrInstanceSettingsDto
            {
                Url = await GetNullableAsync(SettingsKeys.RadarrUrl, cancellationToken),
                ApiKey = new SecretStatus(await store.IsSetAsync(SettingsKeys.RadarrApiKey, cancellationToken)),
                SyncIntervalMinutes = await GetOrDefaultAsync(SettingsKeys.RadarrSyncIntervalMinutes, 15, cancellationToken),
                Defaults = await GetDefaultsAsync(SettingsKeys.RadarrDefaults, cancellationToken),
            },
            Webhooks = new WebhookSettingsDto
            {
                Token = new SecretStatus(await store.IsSetAsync(SettingsKeys.WebhooksToken, cancellationToken)),
            },
            Ui = new UiSettingsDto
            {
                Language = await GetOrDefaultAsync(SettingsKeys.UiLanguage, "en", cancellationToken),
            },
        };
    }

    public async Task UpdateAsync(UpdateSettingsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Watchmode is { } wm)
        {
            await ApplySecretAsync(SettingsKeys.WatchmodeApiKey, wm.ApiKey, cancellationToken);
            if (wm.Plan is not null)
            {
                await store.SetAsync(SettingsKeys.WatchmodePlan, ToJson(wm.Plan), isSecret: false, cancellationToken);
            }

            if (wm.Quota is { } quota)
            {
                await store.SetAsync(SettingsKeys.WatchmodeQuota, ToJson(quota), isSecret: false, cancellationToken);
            }

            if (wm.ReservePercent is { } reserve)
            {
                await store.SetAsync(SettingsKeys.WatchmodeReservePercent, ToJson(reserve), isSecret: false, cancellationToken);
            }

            if (wm.MaxCreditsPerRun is { } max)
            {
                await store.SetAsync(SettingsKeys.WatchmodeMaxCreditsPerRun, ToJson(max), isSecret: false, cancellationToken);
            }
        }

        if (request.Tmdb is { } tmdb)
        {
            await ApplySecretAsync(SettingsKeys.TmdbToken, tmdb.Token, cancellationToken);
        }

        if (request.Omdb is { } omdb)
        {
            await ApplySecretAsync(SettingsKeys.OmdbApiKey, omdb.ApiKey, cancellationToken);
        }

        if (request.Sonarr is { } sonarr)
        {
            if (sonarr.Url is not null)
            {
                await store.SetAsync(SettingsKeys.SonarrUrl, ToJson(NormalizeUrl(sonarr.Url)), isSecret: false, cancellationToken);
            }

            await ApplySecretAsync(SettingsKeys.SonarrApiKey, sonarr.ApiKey, cancellationToken);
            if (sonarr.SyncIntervalMinutes is { } interval)
            {
                await store.SetAsync(SettingsKeys.SonarrSyncIntervalMinutes, ToJson(interval), isSecret: false, cancellationToken);
            }

            if (sonarr.Defaults is { } defaults)
            {
                await store.SetAsync(SettingsKeys.SonarrDefaults, ToJson(defaults), isSecret: false, cancellationToken);
            }
        }

        if (request.Radarr is { } radarr)
        {
            if (radarr.Url is not null)
            {
                await store.SetAsync(SettingsKeys.RadarrUrl, ToJson(NormalizeUrl(radarr.Url)), isSecret: false, cancellationToken);
            }

            await ApplySecretAsync(SettingsKeys.RadarrApiKey, radarr.ApiKey, cancellationToken);
            if (radarr.SyncIntervalMinutes is { } interval)
            {
                await store.SetAsync(SettingsKeys.RadarrSyncIntervalMinutes, ToJson(interval), isSecret: false, cancellationToken);
            }

            if (radarr.Defaults is { } defaults)
            {
                await store.SetAsync(SettingsKeys.RadarrDefaults, ToJson(defaults), isSecret: false, cancellationToken);
            }
        }

        if (request.Ui is { } ui)
        {
            await store.SetAsync(SettingsKeys.UiLanguage, ToJson(ui.Language), isSecret: false, cancellationToken);
        }
    }

    private async Task ApplySecretAsync(string key, string? value, CancellationToken cancellationToken)
    {
        if (value is null)
        {
            return;
        }

        if (value.Length == 0)
        {
            await store.DeleteAsync(key, cancellationToken);
            return;
        }

        await store.SetAsync(key, ToJson(value), isSecret: true, cancellationToken);
    }

    private async Task<T> GetOrDefaultAsync<T>(string key, T defaultValue, CancellationToken cancellationToken)
    {
        var raw = await store.GetRawAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return JsonSerializer.Deserialize<T>(raw, JsonOptions) ?? defaultValue;
    }

    private async Task<string?> GetNullableAsync(string key, CancellationToken cancellationToken)
    {
        var raw = await store.GetRawAsync(key, cancellationToken);
        return string.IsNullOrWhiteSpace(raw) ? null : JsonSerializer.Deserialize<string>(raw, JsonOptions);
    }

    private async Task<ArrDefaultsDto?> GetDefaultsAsync(string key, CancellationToken cancellationToken)
    {
        var raw = await store.GetRawAsync(key, cancellationToken);
        return string.IsNullOrWhiteSpace(raw)
            ? null
            : JsonSerializer.Deserialize<ArrDefaultsDto>(raw, JsonOptions);
    }

    private static string ToJson<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static string NormalizeUrl(string url) => url.Trim().TrimEnd('/');
}
