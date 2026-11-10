using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Integration.Tests;

[CollectionDefinition("ratings-api")]
public sealed class RatingsApiCollection : ICollectionFixture<ApiFactory>;

[Collection("ratings-api")]
public sealed class RatingsApiTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public RatingsApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.WithAdmin();
    }

    [Fact]
    public async Task Put_delete_rating_and_expose_on_detail()
    {
        var titleId = await SeedTitleAsync();

        var put = await _client.PutAsJsonAsync($"/api/v1/titles/{titleId}/rating", new { value = "like" });
        put.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detail = await _client.GetAsync($"/api/v1/titles/{titleId}");
        detail.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await detail.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("rating").GetString().ShouldBe("like");

        var replace = await _client.PutAsJsonAsync($"/api/v1/titles/{titleId}/rating", new { value = "dislike" });
        replace.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
            var rating = await db.Ratings.SingleAsync(r => r.TitleId == titleId);
            rating.Value.ShouldBe(RatingValues.Dislike);
            (await db.RatingEvents.CountAsync(e => e.TitleId == titleId)).ShouldBe(2);
        }

        var delete = await _client.DeleteAsync($"/api/v1/titles/{titleId}/rating");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var after = await _client.GetAsync($"/api/v1/titles/{titleId}");
        var afterBody = await after.Content.ReadFromJsonAsync<JsonElement>();
        afterBody.GetProperty("rating").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Put_when_reco_disabled_returns_409()
    {
        var titleId = await SeedTitleAsync();
        var disable = await _client.PutAsJsonAsync("/api/v1/settings", new { reco = new { enabled = false } });
        disable.StatusCode.ShouldBe(HttpStatusCode.OK);

        var put = await _client.PutAsJsonAsync($"/api/v1/titles/{titleId}/rating", new { value = "like" });
        put.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe("RECO_DISABLED");

        // restore for other tests in this factory if any
        await _client.PutAsJsonAsync("/api/v1/settings", new { reco = new { enabled = true } });
    }

    [Fact]
    public async Task Settings_expose_reco_enabled_default_true()
    {
        var response = await _client.GetAsync("/api/v1/settings");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("reco").GetProperty("enabled").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task List_export_and_import_unknown()
    {
        var titleId = await SeedTitleAsync(tmdbId: 4242, tmdbType: "movie");
        var put = await _client.PutAsJsonAsync($"/api/v1/titles/{titleId}/rating", new { value = "like" });
        put.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var list = await _client.GetAsync("/api/v1/ratings?value=like");
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        var listBody = await list.Content.ReadFromJsonAsync<JsonElement>();
        listBody.GetProperty("total").GetInt32().ShouldBeGreaterThanOrEqualTo(1);

        var export = await _client.GetAsync("/api/v1/ratings/export");
        export.StatusCode.ShouldBe(HttpStatusCode.OK);
        var exportBody = await export.Content.ReadFromJsonAsync<JsonElement>();
        exportBody.GetArrayLength().ShouldBeGreaterThanOrEqualTo(1);

        var import = await _client.PostAsJsonAsync("/api/v1/ratings/import", new
        {
            mode = "merge",
            items = new[]
            {
                new { tmdbType = "movie", tmdbId = 4242, value = "dislike" },
                new { tmdbType = "movie", tmdbId = 99999901, value = "like" },
            },
        });
        import.StatusCode.ShouldBe(HttpStatusCode.OK);
        var importBody = await import.Content.ReadFromJsonAsync<JsonElement>();
        importBody.GetProperty("imported").GetInt32().ShouldBe(1);
        importBody.GetProperty("unknown").GetInt32().ShouldBe(1);

        var detail = await _client.GetAsync($"/api/v1/titles/{titleId}");
        var detailBody = await detail.Content.ReadFromJsonAsync<JsonElement>();
        detailBody.GetProperty("rating").GetString().ShouldBe("dislike");
    }

    private async Task<int> SeedTitleAsync(int? tmdbId = null, string? tmdbType = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
        var now = DateTime.UtcNow;
        var title = new Title
        {
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = $"Rating Api {Guid.NewGuid():N}",
            TmdbId = tmdbId,
            TmdbType = tmdbType,
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Titles.Add(title);
        await db.SaveChangesAsync();
        return title.Id;
    }
}
