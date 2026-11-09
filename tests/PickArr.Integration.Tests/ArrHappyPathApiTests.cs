using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Persistence;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Pickarr.Integration.Tests;

[CollectionDefinition("arr-happy-path-api")]
public sealed class ArrHappyPathApiCollection : ICollectionFixture<ApiFactory>;

[Collection("arr-happy-path-api")]
public sealed class ArrHappyPathApiTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly WireMockServer _server = WireMockServer.Start();
    private readonly string _fixtureRoot = FindFixtureRoot();

    public ArrHappyPathApiTests(ApiFactory factory)
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
    public async Task Add_then_delete_series_through_http()
    {
        _server.Reset();
        MapJson("/api/v3/series", "[]");
        MapFixture("/api/v3/series/lookup", "sonarr/series-lookup.json");
        MapPostFixture("/api/v3/series", "sonarr/series-added.json");
        MapDeleteOk("/api/v3/series/77");

        await ConfigureSonarrAsync();
        var titleId = await SeedSeriesAsync(tvdbId: 121361, tmdbId: 1396);

        var add = await _client.PostAsJsonAsync($"/api/v1/titles/{titleId}/arr", new
        {
            qualityProfileId = 1,
            rootFolderPath = "/tv",
            monitor = "all",
            monitorNewItems = "all",
            seriesType = "standard",
            seasonFolder = true,
            tags = Array.Empty<int>(),
            searchNow = false,
            seasons = new[] { 2 },
        });

        add.StatusCode.ShouldBe(HttpStatusCode.Created);
        var added = await add.Content.ReadFromJsonAsync<JsonElement>();
        added.GetProperty("arrId").GetInt32().ShouldBe(77);
        added.GetProperty("canDelete").GetBoolean().ShouldBeTrue();

        var delete = await _client.DeleteAsync(
            $"/api/v1/titles/{titleId}/arr?deleteFiles=false&addImportExclusion=false");
        delete.StatusCode.ShouldBe(HttpStatusCode.OK);
        var deleted = await delete.Content.ReadFromJsonAsync<JsonElement>();
        deleted.GetProperty("state").GetString().ShouldBe("notInLibrary");
        deleted.GetProperty("canAdd").GetBoolean().ShouldBeTrue();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
        (await db.ArrLibraryItems.CountAsync(i => i.TitleId == titleId)).ShouldBe(0);
        (await db.Titles.CountAsync(t => t.Id == titleId)).ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_without_config_returns_not_configured()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ISettingsStore>();
            await store.DeleteAsync(SettingsKeys.SonarrUrl);
            await store.DeleteAsync(SettingsKeys.SonarrApiKey);
            await store.DeleteAsync(SettingsKeys.RadarrUrl);
            await store.DeleteAsync(SettingsKeys.RadarrApiKey);
        }

        var titleId = await SeedSeriesAsync(tvdbId: 555001, tmdbId: 555001);
        var response = await _client.PostAsync($"/api/v1/titles/{titleId}/arr/refresh", content: null);
        response.StatusCode.ShouldBe((HttpStatusCode)503);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe("ARR_NOT_CONFIGURED");
    }

    [Fact]
    public async Task Add_with_invalid_json_returns_bad_request()
    {
        var titleId = await SeedSeriesAsync(tvdbId: 555002, tmdbId: 555002);
        using var content = new StringContent("{not-json", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/api/v1/titles/{titleId}/arr", content);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe("INVALID_BODY");
    }

    private async Task ConfigureSonarrAsync()
    {
        var put = await _client.PutAsJsonAsync("/api/v1/settings", new
        {
            sonarr = new { url = _server.Urls[0], apiKey = "wiremock-key" },
        });
        put.EnsureSuccessStatusCode();
    }

    private async Task<int> SeedSeriesAsync(int tvdbId, int tmdbId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
        var title = new Title
        {
            MediaType = MediaTypes.Series,
            Category = TitleCategories.Series,
            OriginalTitle = $"Happy Path Series {tvdbId}",
            TvdbId = tvdbId,
            TmdbId = tmdbId,
            TmdbType = "tv",
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Titles.Add(title);
        await db.SaveChangesAsync();
        return title.Id;
    }

    private void MapFixture(string path, string relative)
    {
        var body = File.ReadAllText(Path.Combine(_fixtureRoot, relative));
        MapJson(path, body);
    }

    private void MapJson(string path, string body)
    {
        _server
            .Given(Request.Create().WithPath(path).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
    }

    private void MapPostFixture(string path, string relative)
    {
        var body = File.ReadAllText(Path.Combine(_fixtureRoot, relative));
        _server
            .Given(Request.Create().WithPath(path).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
    }

    private void MapDeleteOk(string path)
    {
        _server
            .Given(Request.Create().WithPath(path).UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(200));
    }

    private static string FindFixtureRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "fixtures");
            if (Directory.Exists(Path.Combine(candidate, "sonarr")))
            {
                return candidate;
            }

            candidate = Path.Combine(dir.FullName, "fixtures");
            if (Directory.Exists(Path.Combine(candidate, "sonarr")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("fixtures/sonarr not found");
    }
}
