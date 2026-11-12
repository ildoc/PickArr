using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Shouldly;

namespace Pickarr.Integration.Tests;

[Collection("api")]
public sealed class SettingsAndHealthTests
{
    private readonly HttpClient _client;

    public SettingsAndHealthTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_endpoints_return_ok()
    {
        (await _client.GetAsync("/health")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _client.GetAsync("/health/ready")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task System_status_reports_product_version()
    {
        var response = await _client.GetAsync("/api/v1/system/status");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var informational = typeof(Pickarr.Api.Endpoints.SystemEndpoints).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "";
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        var expected = plus >= 0 ? informational[..plus] : informational;

        var version = body.GetProperty("version").GetString().ShouldNotBeNull();
        version.ShouldBe(expected);
        version.ShouldNotContain("+");
    }

    [Fact]
    public async Task System_image_updates_returns_check_shape()
    {
        var response = await _client.GetAsync("/api/v1/system/image-updates");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("currentVersion").GetString().ShouldNotBeNullOrWhiteSpace();
        var channel = body.GetProperty("channel").GetString();
        channel.ShouldBeOneOf("release", "development");
        body.TryGetProperty("newestTag", out _).ShouldBeTrue();
        var available = body.GetProperty("updateAvailable");
        (available.ValueKind is JsonValueKind.True or JsonValueKind.False).ShouldBeTrue();
        var checkSucceeded = body.GetProperty("checkSucceeded");
        (checkSucceeded.ValueKind is JsonValueKind.True or JsonValueKind.False).ShouldBeTrue();
        body.GetProperty("source").GetString().ShouldBe("ghcr.io/ildoc/pickarr");
        body.TryGetProperty("checkedAt", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task System_stats_returns_title_and_storage_shape()
    {
        var response = await _client.GetAsync("/api/v1/system/stats");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("totalTitles").GetInt64().ShouldBeGreaterThanOrEqualTo(0);
        body.GetProperty("byProvider").ValueKind.ShouldBe(JsonValueKind.Array);
        body.GetProperty("byCountry").ValueKind.ShouldBe(JsonValueKind.Array);
        body.GetProperty("bySource").ValueKind.ShouldBe(JsonValueKind.Array);
        var storage = body.GetProperty("storage");
        storage.GetProperty("totalBytes").GetInt64().ShouldBeGreaterThanOrEqualTo(0);
        storage.GetProperty("entries").ValueKind.ShouldBe(JsonValueKind.Array);
        storage.GetProperty("entries").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Settings_hides_secrets_and_persists_isSet()
    {
        var put = await _client.PutAsJsonAsync("/api/v1/settings", new
        {
            watchmode = new { apiKey = "wm-test-key" },
        });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await put.Content.ReadAsStringAsync();
        body.ShouldNotContain("wm-test-key");

        var after = JsonSerializer.Deserialize<JsonElement>(body);
        after.GetProperty("watchmode").GetProperty("apiKey").GetProperty("isSet").GetBoolean().ShouldBeTrue();

        var getResponse = await _client.GetAsync("/api/v1/settings");
        var getBody = await getResponse.Content.ReadAsStringAsync();
        getBody.ShouldNotContain("wm-test-key");
        var get = JsonSerializer.Deserialize<JsonElement>(getBody);
        get.GetProperty("watchmode").GetProperty("apiKey").GetProperty("isSet").GetBoolean().ShouldBeTrue();
    }
}
