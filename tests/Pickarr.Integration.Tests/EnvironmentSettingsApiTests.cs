using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Settings;
using Shouldly;

namespace Pickarr.Integration.Tests;

public sealed class EnvSettingsApiFactory : ApiFactory
{
    protected override void ConfigureApplication(IWebHostBuilder builder)
    {
        builder.UseSetting("Pickarr:Settings:watchmode:apiKey", "env-watchmode-key");
        builder.UseSetting("Pickarr:Settings:watchmode:reservePercent", "5");
        builder.UseSetting("Pickarr:Settings:sonarr:url", "http://sonarr.internal:8989/");
        builder.UseSetting("Pickarr:Settings:reco:enabled", "false");
    }
}

[CollectionDefinition("settings-env")]
public sealed class EnvSettingsApiCollection : ICollectionFixture<EnvSettingsApiFactory>;

[Collection("settings-env")]
public sealed class EnvironmentSettingsApiTests
{
    private readonly EnvSettingsApiFactory _factory;
    private readonly HttpClient _client;

    public EnvironmentSettingsApiTests(EnvSettingsApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Declared_settings_are_effective_and_secrets_stay_hidden()
    {
        var response = await _client.GetAsync("/api/v1/settings");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain("env-watchmode-key");

        var json = JsonSerializer.Deserialize<JsonElement>(body);
        json.GetProperty("watchmode").GetProperty("apiKey").GetProperty("isSet").GetBoolean().ShouldBeTrue();
        json.GetProperty("watchmode").GetProperty("quota").GetInt32().ShouldBe(2500);
        json.GetProperty("watchmode").GetProperty("reservePercent").GetInt32().ShouldBe(5);
        json.GetProperty("sonarr").GetProperty("url").GetString().ShouldBe("http://sonarr.internal:8989");
        json.GetProperty("reco").GetProperty("enabled").GetBoolean().ShouldBeFalse();

        var put = await _client.PutAsJsonAsync("/api/v1/settings", new
        {
            watchmode = new { reservePercent = 99, apiKey = "typed-in-the-ui" },
        });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var saved = await put.Content.ReadAsStringAsync();
        saved.ShouldNotContain("env-watchmode-key");
        saved.ShouldNotContain("typed-in-the-ui");
        var after = JsonSerializer.Deserialize<JsonElement>(saved);
        after.GetProperty("watchmode").GetProperty("reservePercent").GetInt32().ShouldBe(5);
        after.GetProperty("watchmode").GetProperty("quota").GetInt32().ShouldBe(2500);
        after.GetProperty("watchmode").GetProperty("apiKey").GetProperty("isSet").GetBoolean().ShouldBeTrue();

        using var scope = _factory.Services.CreateScope();
        var stored = scope.ServiceProvider.GetRequiredService<ProtectedSettingsStore>();
        (await stored.GetRawAsync(SettingsKeys.WatchmodeApiKey)).ShouldBeNull();
        (await stored.GetRawAsync(SettingsKeys.WatchmodeReservePercent)).ShouldBe("10");
    }
}
