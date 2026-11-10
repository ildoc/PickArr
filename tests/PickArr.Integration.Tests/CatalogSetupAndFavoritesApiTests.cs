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

[CollectionDefinition("catalog-setup-favorites-api")]
public sealed class CatalogSetupAndFavoritesApiCollection : ICollectionFixture<ApiFactory>;

[Collection("catalog-setup-favorites-api")]
public sealed class CatalogSetupAndFavoritesApiTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public CatalogSetupAndFavoritesApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.WithAdmin();
    }

    [Fact]
    public async Task Settings_catalogs_get_and_put_round_trip()
    {
        var get = await _client.GetAsync("/api/v1/settings/catalogs");
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        var before = await get.Content.ReadFromJsonAsync<JsonElement>();
        before.GetProperty("plan").GetString().ShouldBe("free");
        before.GetProperty("freeCountryLimit").GetInt32().ShouldBe(3);

        var netflix = before.GetProperty("providers").EnumerateArray()
            .First(p => p.GetProperty("key").GetString() == "netflix");
        var netflixId = netflix.GetProperty("id").GetInt32();

        var put = await _client.PutAsJsonAsync("/api/v1/settings/catalogs", new
        {
            providers = new[]
            {
                new { id = netflixId, enabled = true, countries = new[] { "IT", "US" } },
            },
        });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = await put.Content.ReadFromJsonAsync<JsonElement>();
        var enabled = after.GetProperty("providers").EnumerateArray()
            .First(p => p.GetProperty("id").GetInt32() == netflixId)
            .GetProperty("countries").EnumerateArray()
            .Where(c => c.GetProperty("enabled").GetBoolean())
            .Select(c => c.GetProperty("code").GetString())
            .OrderBy(c => c)
            .ToArray();
        enabled.ShouldBe(["IT", "US"]);
    }

    [Fact]
    public async Task Settings_catalogs_put_rejects_free_plan_country_limit()
    {
        var get = await _client.GetAsync("/api/v1/settings/catalogs");
        var body = await get.Content.ReadFromJsonAsync<JsonElement>();
        var netflixId = body.GetProperty("providers").EnumerateArray()
            .First(p => p.GetProperty("key").GetString() == "netflix")
            .GetProperty("id").GetInt32();

        var put = await _client.PutAsJsonAsync("/api/v1/settings/catalogs", new
        {
            providers = new[]
            {
                new
                {
                    id = netflixId,
                    enabled = true,
                    countries = new[] { "US", "IT", "GB", "CA" },
                },
            },
        });

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await put.Content.ReadFromJsonAsync<JsonElement>();
        error.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain("3 countries");
    }

    [Fact]
    public async Task Favorites_put_delete_are_idempotent_and_genres_types_are_scoped()
    {
        await _client.PutAsJsonAsync("/api/v1/settings", new
        {
            sonarr = new { url = "http://sonarr.local", apiKey = "sonarr-key" },
            radarr = new { url = "http://radarr.local", apiKey = "radarr-key" },
        });

        var seed = await SeedFavoriteScopeAsync();

        var put = await _client.PutAsync($"/api/v1/favorites/{seed.TitleId}", new StringContent(string.Empty));
        put.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var putAgain = await _client.PutAsync($"/api/v1/favorites/{seed.TitleId}", new StringContent(string.Empty));
        putAgain.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var types = await _client.GetAsync("/api/v1/favorites/types");
        types.StatusCode.ShouldBe(HttpStatusCode.OK);
        var typesBody = await types.Content.ReadFromJsonAsync<JsonElement>();
        typesBody.EnumerateArray().Any(t => t.GetProperty("type").GetString() == "movie").ShouldBeTrue();

        var genres = await _client.GetAsync("/api/v1/favorites/genres");
        genres.StatusCode.ShouldBe(HttpStatusCode.OK);
        var genresBody = await genres.Content.ReadFromJsonAsync<JsonElement>();
        genresBody.EnumerateArray().Any(g => g.GetProperty("name").GetString() == "Action").ShouldBeTrue();

        var catalogTypes = await _client.GetAsync($"/api/v1/catalog/sources/{seed.SourceId}/types");
        catalogTypes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var catalogTypesBody = await catalogTypes.Content.ReadFromJsonAsync<JsonElement>();
        catalogTypesBody.EnumerateArray().Any(t => t.GetProperty("type").GetString() == "movie").ShouldBeTrue();

        var catalogGenres = await _client.GetAsync($"/api/v1/catalog/sources/{seed.SourceId}/genres");
        catalogGenres.StatusCode.ShouldBe(HttpStatusCode.OK);
        var catalogGenresBody = await catalogGenres.Content.ReadFromJsonAsync<JsonElement>();
        catalogGenresBody.EnumerateArray().Any(g => g.GetProperty("name").GetString() == "Action").ShouldBeTrue();

        var delete = await _client.DeleteAsync($"/api/v1/favorites/{seed.TitleId}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var deleteAgain = await _client.DeleteAsync($"/api/v1/favorites/{seed.TitleId}");
        deleteAgain.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
            (await db.Favorites.CountAsync(f => f.TitleId == seed.TitleId)).ShouldBe(0);
        }
    }

    private async Task<(int TitleId, int SourceId)> SeedFavoriteScopeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
        var source = await db.CatalogSources.FirstAsync(s => s.CountryCode == "IT");
        source.IsEnabled = true;
        source.BaselineCompletedAt ??= DateTime.UtcNow;
        source.LastFullAt ??= DateTime.UtcNow;

        const string marker = "Catalog Setup Favorite Movie";
        var title = await db.Titles.FirstOrDefaultAsync(t => t.OriginalTitle == marker);
        if (title is null)
        {
            var now = DateTime.UtcNow;
            title = new Title
            {
                MediaType = MediaTypes.Movie,
                Category = TitleCategories.Movie,
                OriginalTitle = marker,
                OriginalTitleNormalized = "catalog setup favorite movie",
                ReleaseYear = 2021,
                MetadataStatus = MetadataStatuses.Ready,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Titles.Add(title);
            await db.SaveChangesAsync();
            title.Localizations.Add(new TitleLocalization
            {
                TitleId = title.Id,
                Language = "it-IT",
                Name = marker,
                NameNormalized = "catalog setup favorite movie",
                FetchedAt = now,
            });
            db.Availabilities.Add(new Availability
            {
                TitleId = title.Id,
                CatalogSourceId = source.Id,
                FirstSeenAt = now,
                LastSeenAt = now,
                AddedAt = now,
            });

            var genre = new Genre();
            db.Genres.Add(genre);
            await db.SaveChangesAsync();
            db.GenreLocalizations.AddRange(
                new GenreLocalization
                {
                    GenreId = genre.Id,
                    Language = "it-IT",
                    Name = "Action",
                },
                new GenreLocalization
                {
                    GenreId = genre.Id,
                    Language = "en-US",
                    Name = "Action",
                });
            db.TitleGenres.Add(new TitleGenre { TitleId = title.Id, GenreId = genre.Id });
            await db.SaveChangesAsync();
        }

        await db.SaveChangesAsync();
        return (title.Id, source.Id);
    }
}
