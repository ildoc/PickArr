using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PickArr.Core.Abstractions;
using PickArr.Core.Settings;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace PickArr.Integration.Tests;

[Collection("api")]
public sealed class ArrConnectionApiTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly WireMockServer _server = WireMockServer.Start();

    public ArrConnectionApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _server.Stop();
        _server.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Test_connection_returns_profiles_without_saving_the_key()
    {
        _server.Reset();
        Map("/api/v3/system/status", """{"version":"4.0.14","appName":"Sonarr"}""");
        Map("/api/v3/qualityprofile", """[{"id":1,"name":"Any"},{"id":4,"name":"HD-1080p"}]""");
        Map("/api/v3/rootfolder", """[{"path":"/tv","freeSpace":10}]""");

        var response = await _client.PostAsJsonAsync("/api/v1/arr/test", new
        {
            kind = "sonarr",
            url = _server.Urls[0],
            apiKey = "probe-key-should-not-save",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("ok").GetBoolean().ShouldBeTrue();
        body.GetProperty("version").GetString().ShouldBe("4.0.14");
        body.GetProperty("qualityProfiles").GetArrayLength().ShouldBe(2);
        body.GetProperty("qualityProfiles")[1].GetProperty("name").GetString().ShouldBe("HD-1080p");
        body.GetProperty("rootFolders")[0].GetProperty("path").GetString().ShouldBe("/tv");

        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ISettingsStore>();
        var savedKey = await store.GetRawAsync(SettingsKeys.SonarrApiKey);
        (savedKey ?? "").ShouldNotContain("probe-key-should-not-save");
        var savedUrl = await store.GetRawAsync(SettingsKeys.SonarrUrl);
        (savedUrl ?? "").ShouldNotContain(_server.Urls[0]);
    }

    [Fact]
    public async Task Test_connection_failure_does_not_invent_profiles()
    {
        _server.Reset();
        _server
            .Given(Request.Create().WithPath("/api/v3/system/status").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(401).WithBody("""{"message":"Unauthorized"}"""));

        var response = await _client.PostAsJsonAsync("/api/v1/arr/test", new
        {
            kind = "radarr",
            url = _server.Urls[0],
            apiKey = "wrong",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("ok").GetBoolean().ShouldBeFalse();
        body.GetProperty("qualityProfiles").GetArrayLength().ShouldBe(0);
        body.GetProperty("rootFolders").GetArrayLength().ShouldBe(0);
    }

    private void Map(string path, string body)
    {
        _server
            .Given(Request.Create().WithPath(path).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
    }
}
