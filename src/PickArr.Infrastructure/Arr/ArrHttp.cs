using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Settings;

namespace Pickarr.Infrastructure.Arr;

internal static class ArrHttp
{
    public static async Task<(string Url, string ApiKey)?> ResolveConfigAsync(
        ISettingsStore settings,
        string urlKey,
        string apiKeyKey,
        CancellationToken cancellationToken)
    {
        var urlRaw = await settings.GetRawAsync(urlKey, cancellationToken);
        var keyRaw = await settings.GetRawAsync(apiKeyKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(urlRaw) || string.IsNullOrWhiteSpace(keyRaw))
        {
            return null;
        }

        var url = JsonSerializer.Deserialize<string>(urlRaw)?.Trim().TrimEnd('/');
        var apiKey = JsonSerializer.Deserialize<string>(keyRaw);
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        return (url, apiKey);
    }

    public static HttpRequestMessage Create(HttpMethod method, string baseUrl, string path, string apiKey)
    {
        var uri = new Uri($"{baseUrl.TrimEnd('/')}/api/v3/{path.TrimStart('/')}");
        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
        return request;
    }

    public static async Task<T> SendAsync<T>(
        HttpClient http,
        HttpRequestMessage request,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Arr call {Url} failed: {Status} {Body}", request.RequestUri, (int)response.StatusCode, Truncate(body));
            throw CreateException((int)response.StatusCode, body);
        }

        return JsonSerializer.Deserialize<T>(body, ArrJson.Options)
               ?? throw new InvalidOperationException("Empty Arr JSON response.");
    }

    public static async Task SendAsync(
        HttpClient http,
        HttpRequestMessage request,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Arr call {Url} failed: {Status} {Body}", request.RequestUri, (int)response.StatusCode, Truncate(body));
            throw CreateException((int)response.StatusCode, body);
        }
    }

    public static ArrClientException CreateException(int statusCode, string body)
    {
        var errors = ParseErrors(body);
        var message = errors.Count > 0
            ? string.Join("; ", errors)
            : Truncate(body);
        return new ArrClientException(statusCode, string.IsNullOrWhiteSpace(message) ? $"Arr HTTP {statusCode}" : message, errors);
    }

    private static IReadOnlyList<string> ParseErrors(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.TryGetProperty("errorMessage", out var msg) && msg.ValueKind == JsonValueKind.String)
                    {
                        list.Add(msg.GetString()!);
                    }
                    else if (item.TryGetProperty("message", out var alt) && alt.ValueKind == JsonValueKind.String)
                    {
                        list.Add(alt.GetString()!);
                    }
                }

                return list;
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("message", out var single)
                && single.ValueKind == JsonValueKind.String)
            {
                return [single.GetString()!];
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return [];
    }

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300] + "…";
}
