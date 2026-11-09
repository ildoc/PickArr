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

[CollectionDefinition("favorites-api")]
public sealed class FavoritesApiCollection : ICollectionFixture<ApiFactory>;

[Collection("favorites-api")]
public sealed class FavoritesApiTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public FavoritesApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task My_list_is_separate_from_the_catalog_source()
    {
        var put = await _client.PutAsJsonAsync("/api/v1/settings", new
        {
            sonarr = new { url = "http://sonarr.local", apiKey = "sonarr-key" },
            radarr = new { url = "http://radarr.local", apiKey = "radarr-key" },
        });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        int italyId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
            var italy = await db.CatalogSources.FirstAsync(s => s.CountryCode == "IT");
            var unitedStates = await db.CatalogSources.FirstAsync(s => s.CountryCode == "US");
            italy.IsEnabled = true;
            unitedStates.IsEnabled = true;
            await db.SaveChangesAsync();
            italyId = italy.Id;

            if (!await db.Titles.AnyAsync(t => t.OriginalTitle == "Favorites Page Split"))
            {
                var now = DateTime.UtcNow;
                var movie = new Title
                {
                    MediaType = MediaTypes.Movie,
                    Category = TitleCategories.Movie,
                    OriginalTitle = "Favorites Page Split",
                    OriginalTitleNormalized = "favorites page split",
                    ReleaseYear = 2022,
                    MetadataStatus = MetadataStatuses.Ready,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.Titles.Add(movie);
                await db.SaveChangesAsync();
                db.Availabilities.Add(new Availability
                {
                    TitleId = movie.Id,
                    CatalogSourceId = unitedStates.Id,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    AddedAt = now,
                });
                db.Favorites.Add(new Favorite { TitleId = movie.Id, CreatedAt = now });
                await db.SaveChangesAsync();
            }
        }

        var catalog = await _client.GetAsync($"/api/v1/catalog/sources/{italyId}/titles?q=favorites%20page%20split");
        catalog.StatusCode.ShouldBe(HttpStatusCode.OK);
        var catalogBody = await catalog.Content.ReadFromJsonAsync<JsonElement>();
        catalogBody.GetProperty("total").GetInt32().ShouldBe(0);

        var flagged = await _client.GetAsync(
            $"/api/v1/catalog/sources/{italyId}/titles?q=favorites%20page%20split&favorite=true");
        var flaggedBody = await flagged.Content.ReadFromJsonAsync<JsonElement>();
        flaggedBody.GetProperty("total").GetInt32().ShouldBe(0);

        var list = await _client.GetAsync("/api/v1/favorites?q=favorites%20page%20split");
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        var listBody = await list.Content.ReadFromJsonAsync<JsonElement>();
        listBody.GetProperty("total").GetInt32().ShouldBe(1);
        var places = listBody.GetProperty("items")[0].GetProperty("places");
        places.EnumerateArray().Any(place => place.GetProperty("countryCode").GetString() == "US").ShouldBeTrue();

        var types = await _client.GetAsync("/api/v1/favorites/types");
        types.StatusCode.ShouldBe(HttpStatusCode.OK);
        var typesBody = await types.Content.ReadFromJsonAsync<JsonElement>();
        typesBody.EnumerateArray().Any(item => item.GetProperty("type").GetString() == "movie").ShouldBeTrue();
    }
}
