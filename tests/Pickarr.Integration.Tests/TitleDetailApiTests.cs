using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Integration.Tests;

[CollectionDefinition("title-detail-api")]
public sealed class TitleDetailApiCollection : ICollectionFixture<ApiFactory>;

[Collection("title-detail-api")]
public sealed class TitleDetailApiTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public TitleDetailApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_title_missing_returns_404()
    {
        var response = await _client.GetAsync("/api/v1/titles/999999");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Seasons_are_newest_first_and_episodes_are_oldest_first()
    {
        var titleId = await SeedSeriesWithCachedSeasonsAsync();

        var seasonsResponse = await _client.GetAsync($"/api/v1/titles/{titleId}/seasons");
        seasonsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var seasons = await seasonsResponse.Content.ReadFromJsonAsync<JsonElement>();
        seasons.GetArrayLength().ShouldBe(2);
        seasons[0].GetProperty("number").GetInt32().ShouldBe(2);
        seasons[1].GetProperty("number").GetInt32().ShouldBe(1);

        var episodesResponse = await _client.GetAsync($"/api/v1/titles/{titleId}/seasons/2/episodes");
        episodesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var episodes = await episodesResponse.Content.ReadFromJsonAsync<JsonElement>();
        episodes.GetArrayLength().ShouldBe(2);
        episodes[0].GetProperty("number").GetInt32().ShouldBe(1);
        episodes[1].GetProperty("number").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task Seasons_for_missing_title_return_404()
    {
        var response = await _client.GetAsync("/api/v1/titles/999999/seasons");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Similar_missing_title_returns_404()
    {
        var response = await _client.GetAsync("/api/v1/titles/999999/similar");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Similar_when_reco_disabled_returns_409()
    {
        var titleId = await SeedSeriesWithCachedSeasonsAsync();
        var disable = await _client.PutAsJsonAsync("/api/v1/settings", new { reco = new { enabled = false } });
        disable.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await _client.GetAsync($"/api/v1/titles/{titleId}/similar");
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe("RECO_DISABLED");

        await _client.PutAsJsonAsync("/api/v1/settings", new { reco = new { enabled = true } });
    }

    private async Task<int> SeedSeriesWithCachedSeasonsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
        const string marker = "Guide Series Cached";
        var existing = await db.Titles.FirstOrDefaultAsync(t => t.OriginalTitle == marker);
        if (existing is not null)
        {
            return existing.Id;
        }

        var now = DateTime.UtcNow;
        var title = new Title
        {
            MediaType = MediaTypes.Series,
            Category = TitleCategories.Series,
            OriginalTitle = marker,
            OriginalTitleNormalized = "guide series cached",
            TmdbId = 4242,
            TmdbType = "tv",
            MetadataStatus = MetadataStatuses.Ready,
            SeasonsJson =
                """
                [
                  {"number":1,"name":"Season 1","episodeCount":2,"airDate":"2020-01-01","overview":null,"episodes":null},
                  {"number":2,"name":"Season 2","episodeCount":2,"airDate":"2024-01-01","overview":null,"episodes":[
                    {"number":3,"name":"Later","airDate":null,"overview":null,"runtimeMinutes":null},
                    {"number":1,"name":"Earlier","airDate":null,"overview":null,"runtimeMinutes":null}
                  ]}
                ]
                """,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Titles.Add(title);
        await db.SaveChangesAsync();
        return title.Id;
    }
}
