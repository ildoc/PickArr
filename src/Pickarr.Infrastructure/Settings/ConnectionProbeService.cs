using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Catalog.Watchmode;
using Pickarr.Infrastructure.Metadata.Tmdb;

namespace Pickarr.Infrastructure.Settings;

public sealed class ConnectionProbeService(
    IHttpClientFactory httpClientFactory,
    ISettingsStore settings,
    IOptions<WatchmodeOptions> watchmodeOptions,
    IOptions<TmdbOptions> tmdbOptions)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ConnectionTestResponse> TestAsync(
        ConnectionTestRequest request,
        CancellationToken cancellationToken = default)
    {
        var service = request.Service?.Trim().ToLowerInvariant();
        return service switch
        {
            "watchmode" => await TestWatchmodeAsync(request.ApiKey, cancellationToken),
            "tmdb" => await TestTmdbAsync(request.ApiKey, cancellationToken),
            "omdb" => await TestOmdbAsync(request.ApiKey, cancellationToken),
            _ => new ConnectionTestResponse { Ok = false, Detail = "Unknown service." },
        };
    }

    private async Task<ConnectionTestResponse> TestWatchmodeAsync(string? draftKey, CancellationToken cancellationToken)
    {
        var apiKey = await ResolveSecretAsync(draftKey, SettingsKeys.WatchmodeApiKey, cancellationToken);
        if (apiKey is null)
        {
            return new ConnectionTestResponse { Ok = false, Detail = "API key is not set." };
        }

        var baseUrl = watchmodeOptions.Value.BaseUrl.TrimEnd('/') + "/";
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(baseUrl), "sources/"));
        request.Headers.TryAddWithoutValidation("X-API-Key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await httpClientFactory.CreateClient(nameof(ConnectionProbeService))
                .SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new ConnectionTestResponse { Ok = true, Detail = "Watchmode accepted the API key." };
            }

            return new ConnectionTestResponse
            {
                Ok = false,
                Detail = $"Watchmode returned {(int)response.StatusCode} {response.ReasonPhrase}.".Trim(),
            };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResponse { Ok = false, Detail = ex.Message };
        }
    }

    private async Task<ConnectionTestResponse> TestTmdbAsync(string? draftKey, CancellationToken cancellationToken)
    {
        var token = await ResolveSecretAsync(draftKey, SettingsKeys.TmdbApiKey, cancellationToken);
        if (token is null)
        {
            return new ConnectionTestResponse { Ok = false, Detail = "API key is not set." };
        }

        var baseUrl = tmdbOptions.Value.BaseUrl.TrimEnd('/') + "/";
        var uri = new Uri(new Uri(baseUrl), "configuration");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        TmdbAuthHandler.ApplyCredential(request, token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await httpClientFactory.CreateClient(nameof(ConnectionProbeService))
                .SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new ConnectionTestResponse { Ok = true, Detail = "TMDB accepted the API key." };
            }

            return new ConnectionTestResponse
            {
                Ok = false,
                Detail = $"TMDB returned {(int)response.StatusCode} {response.ReasonPhrase}.".Trim(),
            };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResponse { Ok = false, Detail = ex.Message };
        }
    }

    private async Task<ConnectionTestResponse> TestOmdbAsync(string? draftKey, CancellationToken cancellationToken)
    {
        var apiKey = await ResolveSecretAsync(draftKey, SettingsKeys.OmdbApiKey, cancellationToken);
        if (apiKey is null)
        {
            return new ConnectionTestResponse { Ok = false, Detail = "API key is not set." };
        }

        var uri = new Uri(
            $"https://www.omdbapi.com/?apikey={Uri.EscapeDataString(apiKey)}&i=tt3896198");
        try
        {
            using var response = await httpClientFactory.CreateClient(nameof(ConnectionProbeService))
                .GetAsync(uri, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ConnectionTestResponse
                {
                    Ok = false,
                    Detail = $"OMDb returned {(int)response.StatusCode} {response.ReasonPhrase}.".Trim(),
                };
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            if (doc.RootElement.TryGetProperty("Response", out var responseFlag)
                && string.Equals(responseFlag.GetString(), "False", StringComparison.OrdinalIgnoreCase))
            {
                var error = doc.RootElement.TryGetProperty("Error", out var err)
                    ? err.GetString()
                    : "Invalid API key.";
                return new ConnectionTestResponse { Ok = false, Detail = error ?? "Invalid API key." };
            }

            return new ConnectionTestResponse { Ok = true, Detail = "OMDb accepted the API key." };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResponse { Ok = false, Detail = ex.Message };
        }
    }

    private async Task<string?> ResolveSecretAsync(
        string? draftKey,
        string settingsKey,
        CancellationToken cancellationToken)
    {
        var trimmed = draftKey?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed;
        }

        var raw = await settings.GetRawAsync(settingsKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return JsonSerializer.Deserialize<string>(raw, JsonOptions)?.Trim();
    }
}
