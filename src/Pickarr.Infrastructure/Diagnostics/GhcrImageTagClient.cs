using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pickarr.Core.Abstractions;

namespace Pickarr.Infrastructure.Diagnostics;

public sealed class GhcrImageTagClient(
    HttpClient http,
    IOptions<GhcrOptions> options,
    ILogger<GhcrImageTagClient> logger) : IImageTagSource
{
    public async Task<ImageTagListResult> ListTagsAsync(CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var repository = opts.Repository.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(repository))
        {
            return ImageTagListResult.Failed();
        }

        try
        {
            var token = await FetchAnonymousTokenAsync(repository, cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                logger.LogWarning("GHCR anonymous token was empty for {Repository}", repository);
                return ImageTagListResult.Failed();
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/v2/{repository}/tags/list?n=1000");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "GHCR tags/list failed for {Repository}: {StatusCode}",
                    repository,
                    (int)response.StatusCode);
                return ImageTagListResult.Failed();
            }

            var body = await response.Content.ReadFromJsonAsync<TagsListResponse>(cancellationToken);
            if (body?.Tags is null)
            {
                logger.LogWarning("GHCR tags/list returned no tags array for {Repository}", repository);
                return ImageTagListResult.Failed();
            }

            return ImageTagListResult.Ok(body.Tags);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("GHCR tags request timed out for {Repository}", repository);
            return ImageTagListResult.Failed();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "GHCR tags request failed for {Repository}", repository);
            return ImageTagListResult.Failed();
        }
    }

    private async Task<string?> FetchAnonymousTokenAsync(string repository, CancellationToken cancellationToken)
    {
        // Keep '/' literal in scope; EscapeDataString would encode it and GHCR rejects the token request.
        var path = $"/token?service=ghcr.io&scope=repository:{repository}:pull";
        using var response = await http.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "GHCR token request failed for {Repository}: {StatusCode}",
                repository,
                (int)response.StatusCode);
            return null;
        }

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        return body?.Token;
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }

    private sealed class TagsListResponse
    {
        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }
    }
}
