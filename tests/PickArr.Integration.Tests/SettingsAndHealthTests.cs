using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace PickArr.Integration.Tests;

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
