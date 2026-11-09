using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Persistence;
using Shouldly;

namespace PickArr.Integration.Tests;

[CollectionDefinition("arr-action-api")]
public sealed class ArrActionApiCollection : ICollectionFixture<ApiFactory>;

[Collection("arr-action-api")]
public sealed class ArrActionApiTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ArrActionApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Delete_requires_explicit_query_params()
    {
        var titleId = await SeedSeriesAsync();

        var missing = await _client.DeleteAsync($"/api/v1/titles/{titleId}/arr");
        missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var partial = await _client.DeleteAsync($"/api/v1/titles/{titleId}/arr?deleteFiles=false");
        partial.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_without_config_returns_not_configured()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ISettingsStore>();
            await store.DeleteAsync(SettingsKeys.SonarrUrl);
            await store.DeleteAsync(SettingsKeys.SonarrApiKey);
        }

        var titleId = await SeedSeriesAsync();
        var response = await _client.PostAsJsonAsync($"/api/v1/titles/{titleId}/arr", new
        {
            qualityProfileId = 1,
            rootFolderPath = "/tv",
            monitor = "all",
            monitorNewItems = "all",
            seriesType = "standard",
            seasonFolder = true,
            tags = Array.Empty<int>(),
            searchNow = false,
        });

        response.StatusCode.ShouldBe((HttpStatusCode)503);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe("ARR_NOT_CONFIGURED");
    }

    [Fact]
    public async Task Add_without_series_ids_returns_unmapped()
    {
        var titleId = await SeedSeriesWithoutIdsAsync();
        await ConfigureSonarrAsync();

        var response = await _client.PostAsJsonAsync($"/api/v1/titles/{titleId}/arr", new
        {
            qualityProfileId = 1,
            rootFolderPath = "/tv",
            monitor = "all",
            monitorNewItems = "all",
            seriesType = "standard",
            seasonFolder = true,
            tags = Array.Empty<int>(),
            searchNow = false,
        });

        response.StatusCode.ShouldBe((HttpStatusCode)422);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe("ARR_ID_UNMAPPED");
    }

    private async Task ConfigureSonarrAsync()
    {
        var put = await _client.PutAsJsonAsync("/api/v1/settings", new
        {
            sonarr = new { url = "http://sonarr.local", apiKey = "sonarr-key" },
        });
        put.EnsureSuccessStatusCode();
    }

    private async Task<int> SeedSeriesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var stamp = Random.Shared.Next(100_000, 999_999);
        var title = new Title
        {
            MediaType = MediaTypes.Series,
            Category = TitleCategories.Series,
            OriginalTitle = $"Arr Action Series {stamp}",
            TvdbId = stamp,
            TmdbId = stamp,
            TmdbType = "tv",
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Titles.Add(title);
        await db.SaveChangesAsync();
        return title.Id;
    }

    private async Task<int> SeedSeriesWithoutIdsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var stamp = Random.Shared.Next(1_000_000, 9_999_999);
        var title = new Title
        {
            MediaType = MediaTypes.Series,
            Category = TitleCategories.Series,
            OriginalTitle = $"No Ids Series {stamp}",
            TvdbId = null,
            TmdbId = null,
            ImdbId = null,
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Titles.Add(title);
        await db.SaveChangesAsync();
        return title.Id;
    }
}
