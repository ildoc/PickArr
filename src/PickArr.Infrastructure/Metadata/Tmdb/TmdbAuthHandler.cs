using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PickArr.Core.Abstractions;
using PickArr.Core.Settings;

namespace PickArr.Infrastructure.Metadata.Tmdb;

public sealed class TmdbAuthHandler(IServiceScopeFactory scopeFactory) : DelegatingHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsStore>();
        var raw = await settings.GetRawAsync(SettingsKeys.TmdbToken, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("TMDB token is not configured.");
        }

        var token = JsonSerializer.Deserialize<string>(raw, JsonOptions)?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("TMDB token is not configured.");
        }

        ApplyCredential(request, token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await base.SendAsync(request, cancellationToken);
    }

    internal static void ApplyCredential(HttpRequestMessage request, string token)
    {
        if (IsReadAccessToken(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return;
        }

        request.RequestUri = WithApiKey(request.RequestUri, token);
    }

    internal static bool IsReadAccessToken(string token) =>
        token.Count(static c => c == '.') == 2 && token.Length > 40;

    internal static Uri WithApiKey(Uri? uri, string apiKey)
    {
        var current = uri?.OriginalString ?? "";
        var separator = current.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var next = current + separator + "api_key=" + Uri.EscapeDataString(apiKey);
        return uri is { IsAbsoluteUri: true }
            ? new Uri(next, UriKind.Absolute)
            : new Uri(next, UriKind.Relative);
    }
}
