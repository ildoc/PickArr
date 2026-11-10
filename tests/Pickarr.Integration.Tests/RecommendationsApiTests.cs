using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Recommendations;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Integration.Tests;

[CollectionDefinition("recommendations-api")]
public sealed class RecommendationsApiCollection : ICollectionFixture<ApiFactory>;

[Collection("recommendations-api")]
public sealed class RecommendationsApiTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public RecommendationsApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.WithAdmin();
    }

    [Fact]
    public async Task Recommendations_scopes_exclude_rated_include_removed_and_status_cold()
    {
        var seed = await SeedAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var build = scope.ServiceProvider.GetRequiredService<IRecoBuildService>();
            var version = await build.BuildAsync();
            version.ShouldBeGreaterThan(0);
        }

        var missingSource = await _client.GetAsync("/api/v1/recommendations?scope=source");
        missingSource.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var sourceScope = await _client.GetAsync(
            $"/api/v1/recommendations?scope=source&sourceId={seed.NetflixItId}");
        sourceScope.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sourceBody = await sourceScope.Content.ReadFromJsonAsync<JsonElement>();
        // Seed includes one Like → N_eff=1 keeps stage cold (<5).
        sourceBody.GetProperty("stage").GetString().ShouldBe(RecoLearning.StageCold);
        var sourceItems = sourceBody.GetProperty("items").EnumerateArray().ToList();
        sourceItems.ShouldContain(i => i.GetProperty("id").GetInt32() == seed.RemovedTitleId);
        // Rated title excluded even though same TitleId would match across providers.
        sourceItems.ShouldNotContain(i => i.GetProperty("id").GetInt32() == seed.RatedTitleId);

        var removedItem = sourceItems.Single(i => i.GetProperty("id").GetInt32() == seed.RemovedTitleId);
        var removedPlace = removedItem.GetProperty("places")[0];
        removedPlace.GetProperty("removedAt").ValueKind.ShouldNotBe(JsonValueKind.Null);

        var enabledScope = await _client.GetAsync(
            $"/api/v1/recommendations?scope=enabled&sourceId={seed.NetflixItId}");
        enabledScope.StatusCode.ShouldBe(HttpStatusCode.OK);
        var enabledBody = await enabledScope.Content.ReadFromJsonAsync<JsonElement>();
        var enabledIds = enabledBody.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32())
            .ToHashSet();
        enabledIds.ShouldContain(seed.RemovedTitleId);
        enabledIds.ShouldContain(seed.PrimeOnlyTitleId);
        enabledIds.ShouldNotContain(seed.RatedTitleId);

        // Cross-provider: rate the Netflix title via PUT, ensure it disappears from both scopes.
        var rate = await _client.PutAsJsonAsync(
            $"/api/v1/titles/{seed.RemovedTitleId}/rating",
            new { value = "like" });
        rate.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRecoBuildService>().BuildAsync();
        }

        var afterVote = await _client.GetAsync(
            $"/api/v1/recommendations?scope=enabled&sourceId={seed.NetflixItId}");
        var afterIds = (await afterVote.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32())
            .ToHashSet();
        afterIds.ShouldNotContain(seed.RemovedTitleId);
        afterIds.ShouldContain(seed.PrimeOnlyTitleId);

        var status = await _client.GetAsync("/api/v1/recommendations/status");
        status.StatusCode.ShouldBe(HttpStatusCode.OK);
        var statusBody = await status.Content.ReadFromJsonAsync<JsonElement>();
        statusBody.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        statusBody.GetProperty("stage").GetString().ShouldBe(RecoLearning.StageCold);
        // Seed Like + UI Like after rebuild (sibling tests may add more votes in this collection).
        statusBody.GetProperty("effectiveN").GetDouble().ShouldBeGreaterThanOrEqualTo(2);
        statusBody.GetProperty("version").GetInt32().ShouldBeGreaterThan(0);
        statusBody.GetProperty("pool").GetProperty("titles").GetInt32().ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Similar_returns_enabled_neighbour_with_places_and_excludes_rated()
    {
        var seed = await SeedSimilarAsync();

        var response = await _client.GetAsync(
            $"/api/v1/titles/{seed.QueryTitleId}/similar?sourceId={seed.NetflixItId}&limit=10");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        items.ShouldContain(i => i.GetProperty("id").GetInt32() == seed.PrimeNeighbourId);
        items.ShouldNotContain(i => i.GetProperty("id").GetInt32() == seed.RatedNeighbourId);
        items.ShouldNotContain(i => i.GetProperty("id").GetInt32() == seed.QueryTitleId);

        var prime = items.Single(i => i.GetProperty("id").GetInt32() == seed.PrimeNeighbourId);
        prime.GetProperty("places").GetArrayLength().ShouldBeGreaterThan(0);
        prime.GetProperty("reco").GetProperty("similarity").GetDouble().ShouldBeGreaterThan(0);
    }

    private async Task<Seed> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();

        var netflix = await db.Providers.FirstAsync(p => p.Key == "netflix");
        var prime = await db.Providers.FirstAsync(p => p.Key == "prime-video");
        netflix.IsEnabled = true;
        prime.IsEnabled = true;

        var netflixIt = await db.CatalogSources.FirstAsync(s =>
            s.ProviderId == netflix.Id && s.CountryCode == "IT");
        var primeIt = await db.CatalogSources.FirstAsync(s =>
            s.ProviderId == prime.Id && s.CountryCode == "IT");
        netflixIt.IsEnabled = true;
        primeIt.IsEnabled = true;
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var removed = new Title
        {
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = $"Reco Removed {Guid.NewGuid():N}",
            VoteTmdb = 8.5,
            VoteTmdbCount = 2000,
            Popularity = 40,
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var rated = new Title
        {
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = $"Reco Rated {Guid.NewGuid():N}",
            VoteTmdb = 7.0,
            VoteTmdbCount = 500,
            Popularity = 20,
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var primeOnly = new Title
        {
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = $"Reco Prime {Guid.NewGuid():N}",
            VoteTmdb = 9.0,
            VoteTmdbCount = 4000,
            Popularity = 70,
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Titles.AddRange(removed, rated, primeOnly);
        await db.SaveChangesAsync();

        db.Availabilities.AddRange(
            new Availability
            {
                TitleId = removed.Id,
                CatalogSourceId = netflixIt.Id,
                FirstSeenAt = now,
                LastSeenAt = now,
                AddedAt = now.AddDays(-40),
                RemovedAt = now.AddDays(-1),
            },
            new Availability
            {
                TitleId = rated.Id,
                CatalogSourceId = netflixIt.Id,
                FirstSeenAt = now,
                LastSeenAt = now,
                AddedAt = now,
            },
            new Availability
            {
                TitleId = rated.Id,
                CatalogSourceId = primeIt.Id,
                FirstSeenAt = now,
                LastSeenAt = now,
                AddedAt = now,
            },
            new Availability
            {
                TitleId = primeOnly.Id,
                CatalogSourceId = primeIt.Id,
                FirstSeenAt = now,
                LastSeenAt = now,
                AddedAt = now,
            });

        var adminId = await db.Profiles.Where(profile => profile.IsAdmin).Select(profile => profile.Id).SingleAsync();
        db.Ratings.Add(new Rating
        {
            ProfileId = adminId,
            TitleId = rated.Id,
            Value = RatingValues.Like,
            RatedAt = now,
        });
        await db.SaveChangesAsync();

        return new Seed(netflixIt.Id, removed.Id, rated.Id, primeOnly.Id);
    }

    private async Task<SimilarSeed> SeedSimilarAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();

        var netflix = await db.Providers.FirstAsync(p => p.Key == "netflix");
        var prime = await db.Providers.FirstAsync(p => p.Key == "prime-video");
        netflix.IsEnabled = true;
        prime.IsEnabled = true;

        var netflixIt = await db.CatalogSources.FirstAsync(s =>
            s.ProviderId == netflix.Id && s.CountryCode == "IT");
        var primeIt = await db.CatalogSources.FirstAsync(s =>
            s.ProviderId == prime.Id && s.CountryCode == "IT");
        netflixIt.IsEnabled = true;
        primeIt.IsEnabled = true;

        if (!await db.Genres.AnyAsync(g => g.Id == 878))
        {
            db.Genres.Add(new Genre { Id = 878 });
            db.GenreLocalizations.Add(new GenreLocalization
            {
                GenreId = 878,
                Language = "en-US",
                Name = "Science Fiction",
            });
        }

        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var query = new Title
        {
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = $"Similar Query {Guid.NewGuid():N}",
            OriginalLanguage = "en",
            ReleaseYear = 2020,
            VoteTmdb = 7.5,
            VoteTmdbCount = 100,
            Popularity = 10,
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var primeNeighbour = new Title
        {
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = $"Similar Prime {Guid.NewGuid():N}",
            OriginalLanguage = "en",
            ReleaseYear = 2021,
            VoteTmdb = 7.0,
            VoteTmdbCount = 80,
            Popularity = 8,
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var ratedNeighbour = new Title
        {
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = $"Similar Rated {Guid.NewGuid():N}",
            OriginalLanguage = "en",
            ReleaseYear = 2019,
            VoteTmdb = 6.5,
            VoteTmdbCount = 50,
            Popularity = 5,
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Titles.AddRange(query, primeNeighbour, ratedNeighbour);
        await db.SaveChangesAsync();

        db.TitleGenres.AddRange(
            new TitleGenre { TitleId = query.Id, GenreId = 878 },
            new TitleGenre { TitleId = primeNeighbour.Id, GenreId = 878 },
            new TitleGenre { TitleId = ratedNeighbour.Id, GenreId = 878 });

        db.Availabilities.AddRange(
            new Availability
            {
                TitleId = query.Id,
                CatalogSourceId = netflixIt.Id,
                FirstSeenAt = now,
                LastSeenAt = now,
                AddedAt = now,
            },
            new Availability
            {
                TitleId = primeNeighbour.Id,
                CatalogSourceId = primeIt.Id,
                FirstSeenAt = now,
                LastSeenAt = now,
                AddedAt = now,
            },
            new Availability
            {
                TitleId = ratedNeighbour.Id,
                CatalogSourceId = netflixIt.Id,
                FirstSeenAt = now,
                LastSeenAt = now,
                AddedAt = now,
            });

        var adminId = await db.Profiles.Where(profile => profile.IsAdmin).Select(profile => profile.Id).SingleAsync();
        db.Ratings.Add(new Rating
        {
            ProfileId = adminId,
            TitleId = ratedNeighbour.Id,
            Value = RatingValues.Like,
            RatedAt = now,
        });
        await db.SaveChangesAsync();

        return new SimilarSeed(netflixIt.Id, query.Id, primeNeighbour.Id, ratedNeighbour.Id);
    }

    private sealed record Seed(int NetflixItId, int RemovedTitleId, int RatedTitleId, int PrimeOnlyTitleId);

    private sealed record SimilarSeed(
        int NetflixItId,
        int QueryTitleId,
        int PrimeNeighbourId,
        int RatedNeighbourId);
}
