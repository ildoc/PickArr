using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Infrastructure.Persistence;
using Shouldly;

namespace PickArr.Integration.Tests;

[CollectionDefinition("watched-api")]
public sealed class WatchedApiCollection : ICollectionFixture<ApiFactory>;

[Collection("watched-api")]
public sealed class WatchedApiTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public WatchedApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Put_delete_watched_and_expose_on_detail()
    {
        var titleId = await SeedTitleAsync();

        var put = await _client.PutAsync($"/api/v1/titles/{titleId}/watched", null);
        put.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detail = await _client.GetAsync($"/api/v1/titles/{titleId}");
        detail.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await detail.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isWatched").GetBoolean().ShouldBeTrue();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
            (await db.Watched.AnyAsync(w => w.TitleId == titleId)).ShouldBeTrue();
        }

        var delete = await _client.DeleteAsync($"/api/v1/titles/{titleId}/watched");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var after = await _client.GetAsync($"/api/v1/titles/{titleId}");
        var afterBody = await after.Content.ReadFromJsonAsync<JsonElement>();
        afterBody.GetProperty("isWatched").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Put_unknown_title_returns_404()
    {
        var put = await _client.PutAsync("/api/v1/titles/99999901/watched", null);
        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<int> SeedTitleAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var now = DateTime.UtcNow;
        var title = new Title
        {
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = $"Watched Api {Guid.NewGuid():N}",
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Titles.Add(title);
        await db.SaveChangesAsync();
        return title.Id;
    }
}
