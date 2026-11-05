using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PickArr.Core.Abstractions;
using PickArr.Core.Settings;

namespace PickArr.Infrastructure.Catalog.Watchmode;

public sealed class WatchmodeAuthHandler(IServiceScopeFactory scopeFactory) : DelegatingHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var settingsStore = scope.ServiceProvider.GetRequiredService<ISettingsStore>();

        var raw = await settingsStore.GetRawAsync(SettingsKeys.WatchmodeApiKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("Watchmode API key is not configured.");
        }

        var apiKey = JsonSerializer.Deserialize<string>(raw, JsonOptions);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Watchmode API key is not configured.");
        }

        request.Headers.Remove("X-API-Key");
        request.Headers.TryAddWithoutValidation("X-API-Key", apiKey);

        // Never put the key in the URI (legacy apiKey query).
        if (request.RequestUri is not null &&
            !string.IsNullOrEmpty(request.RequestUri.Query) &&
            request.RequestUri.Query.Contains("apiKey=", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Watchmode requests must not include apiKey in the query string.");
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await base.SendAsync(request, cancellationToken);
    }
}
