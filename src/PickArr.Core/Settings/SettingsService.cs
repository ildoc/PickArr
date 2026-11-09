using System.Text.Json;
using PickArr.Core.Abstractions;
using PickArr.Core.Recommendations;

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
                ApiKey = new SecretStatus(await store.IsSetAsync(SettingsKeys.TmdbApiKey, cancellationToken)),
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
            Reco = new RecoSettingsDto
            {
                Enabled = await GetOrDefaultAsync(SettingsKeys.RecoEnabled, true, cancellationToken),
                UseImplicit = await GetOrDefaultAsync(SettingsKeys.RecoUseImplicit, true, cancellationToken),
                EmbeddingsEnabled = await GetOrDefaultAsync(SettingsKeys.RecoEmbeddingsEnabled, true, cancellationToken),
                ExcludeInLibrary = await GetOrDefaultAsync(SettingsKeys.RecoExcludeInLibrary, true, cancellationToken),
                N0 = await GetOrDefaultAsync(SettingsKeys.RecoN0, 30, cancellationToken),
                WeightLike = await GetOrDefaultAsync(SettingsKeys.RecoWeightLike, 1.0, cancellationToken),
                WeightDislike = await GetOrDefaultAsync(SettingsKeys.RecoWeightDislike, -1.0, cancellationToken),
                WeightFavorite = await GetOrDefaultAsync(SettingsKeys.RecoWeightFavorite, 0.5, cancellationToken),
                WeightArrAdd = await GetOrDefaultAsync(SettingsKeys.RecoWeightArrAdd, 0.6, cancellationToken),
                WeightDownloaded = await GetOrDefaultAsync(SettingsKeys.RecoWeightDownloaded, 0.4, cancellationToken),
                MmrLambda = await GetOrDefaultAsync(SettingsKeys.RecoMmrLambda, 0.7, cancellationToken),
                ExploreRate = await GetOrDefaultAsync(SettingsKeys.RecoExploreRate, RecoExplore.DefaultRate, cancellationToken),
                MlMinRatings = await GetOrDefaultAsync(SettingsKeys.RecoMlMinRatings, RecoLevel3.DefaultMinRatings, cancellationToken),
                EventsEnabled = await GetOrDefaultAsync(SettingsKeys.RecoEventsEnabled, false, cancellationToken),
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
            await ApplySecretAsync(SettingsKeys.TmdbApiKey, tmdb.ApiKey, cancellationToken);
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

        if (request.Reco is { } reco)
        {
            if (reco.Enabled is { } enabled)
            {
                await store.SetAsync(SettingsKeys.RecoEnabled, ToJson(enabled), isSecret: false, cancellationToken);
            }

            if (reco.UseImplicit is { } useImplicit)
            {
                await store.SetAsync(SettingsKeys.RecoUseImplicit, ToJson(useImplicit), isSecret: false, cancellationToken);
            }

            if (reco.EmbeddingsEnabled is { } embeddingsEnabled)
            {
                await store.SetAsync(SettingsKeys.RecoEmbeddingsEnabled, ToJson(embeddingsEnabled), isSecret: false, cancellationToken);
            }

            if (reco.ExcludeInLibrary is { } exclude)
            {
                await store.SetAsync(SettingsKeys.RecoExcludeInLibrary, ToJson(exclude), isSecret: false, cancellationToken);
            }

            if (reco.N0 is { } n0)
            {
                await store.SetAsync(SettingsKeys.RecoN0, ToJson(n0), isSecret: false, cancellationToken);
            }

            if (reco.WeightLike is { } like)
            {
                await store.SetAsync(SettingsKeys.RecoWeightLike, ToJson(like), isSecret: false, cancellationToken);
            }

            if (reco.WeightDislike is { } dislike)
            {
                await store.SetAsync(SettingsKeys.RecoWeightDislike, ToJson(dislike), isSecret: false, cancellationToken);
            }

            if (reco.WeightFavorite is { } favorite)
            {
                await store.SetAsync(SettingsKeys.RecoWeightFavorite, ToJson(favorite), isSecret: false, cancellationToken);
            }

            if (reco.WeightArrAdd is { } arrAdd)
            {
                await store.SetAsync(SettingsKeys.RecoWeightArrAdd, ToJson(arrAdd), isSecret: false, cancellationToken);
            }

            if (reco.WeightDownloaded is { } downloaded)
            {
                await store.SetAsync(SettingsKeys.RecoWeightDownloaded, ToJson(downloaded), isSecret: false, cancellationToken);
            }

            if (reco.MmrLambda is { } mmrLambda)
            {
                await store.SetAsync(SettingsKeys.RecoMmrLambda, ToJson(mmrLambda), isSecret: false, cancellationToken);
            }

            if (reco.ExploreRate is { } exploreRate)
            {
                await store.SetAsync(SettingsKeys.RecoExploreRate, ToJson(exploreRate), isSecret: false, cancellationToken);
            }

            if (reco.MlMinRatings is { } minRatings)
            {
                await store.SetAsync(SettingsKeys.RecoMlMinRatings, ToJson(minRatings), isSecret: false, cancellationToken);
            }

            if (reco.EventsEnabled is { } eventsEnabled)
            {
                await store.SetAsync(SettingsKeys.RecoEventsEnabled, ToJson(eventsEnabled), isSecret: false, cancellationToken);
            }
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
