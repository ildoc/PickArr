using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Integration.Tests;

[CollectionDefinition("reco-admin-api")]
public sealed class RecoAdminApiCollection : ICollectionFixture<ApiFactory>;

/// <summary>Own factory — reset/onboarding mutate global ratings and recommendations.</summary>
[Collection("reco-admin-api")]
public sealed class RecoAdminApiTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public RecoAdminApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Onboarding_returns_pool_titles_and_batch_post_accepts()
    {
        var seed = await SeedAsync();

        var get = await _client.GetAsync(
            $"/api/v1/recommendations/onboarding?sourceId={seed.NetflixItId}");
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await get.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Count.ShouldBeGreaterThan(0);
        items.ShouldNotContain(i => i.GetProperty("id").GetInt32() == seed.RatedTitleId);

        var pick = items[0].GetProperty("id").GetInt32();
        var post = await _client.PostAsJsonAsync(
            "/api/v1/ratings/onboarding",
            new[] { new { titleId = pick, value = "like" } });
        post.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
        (await db.Ratings.AnyAsync(r => r.TitleId == pick && r.Value == RatingValues.Like)).ShouldBeTrue();
    }

    [Fact]
    public async Task Onboarding_exclude_skips_previously_shown_titles()
    {
        var seed = await SeedAsync();

        var first = await _client.GetAsync(
            $"/api/v1/recommendations/onboarding?sourceId={seed.NetflixItId}");
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var firstIds = firstBody.GetProperty("items")
            .EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32())
            .ToList();
        firstIds.Count.ShouldBeGreaterThan(0);

        var exclude = string.Join(',', firstIds);
        var second = await _client.GetAsync(
            $"/api/v1/recommendations/onboarding?sourceId={seed.NetflixItId}&exclude={exclude}");
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        var secondIds = secondBody.GetProperty("items")
            .EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32())
            .ToList();

        secondIds.ShouldNotContain(id => firstIds.Contains(id));
        secondIds.ShouldNotContain(seed.RatedTitleId);
    }

    [Fact]
    public async Task Admin_reset_derived_returns_accepted_and_clears_recommendations()
    {
        var seed = await SeedAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRecoBuildService>().BuildAsync();
        }

        var reset = await _client.PostAsync(
            "/api/v1/admin/recommendations/reset?includeRatings=false",
            null);
        reset.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
            (await db.Recommendations.CountAsync()).ShouldBe(0);
            (await db.Ratings.CountAsync(r => r.TitleId == seed.RatedTitleId)).ShouldBe(1);
        }

        var rebuild = await _client.PostAsync("/api/v1/admin/recommendations/rebuild", null);
        rebuild.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var rebuildBody = await rebuild.Content.ReadFromJsonAsync<JsonElement>();
        rebuildBody.GetProperty("hangfireJobId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Admin_evaluate_queues_job_and_latest_report_is_readable_after_run()
    {
        await SeedAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRecoEvalService>().EvaluateAsync();
        }

        var queued = await _client.PostAsync("/api/v1/admin/recommendations/evaluate", null);
        queued.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var latest = await _client.GetAsync("/api/v1/admin/recommendations/evaluations/latest");
        latest.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await latest.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("ratingCount").GetInt32().ShouldBeGreaterThan(0);
        body.GetProperty("lowSampleWarning").GetBoolean().ShouldBeTrue();
        body.GetProperty("level3Active").GetBoolean().ShouldBeFalse();
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
            OriginalTitle = $"RecoAdmin Removed {Guid.NewGuid():N}",
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
            OriginalTitle = $"RecoAdmin Rated {Guid.NewGuid():N}",
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
            OriginalTitle = $"RecoAdmin Prime {Guid.NewGuid():N}",
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
                TitleId = primeOnly.Id,
                CatalogSourceId = primeIt.Id,
                FirstSeenAt = now,
                LastSeenAt = now,
                AddedAt = now,
            });

        db.Ratings.Add(new Rating
        {
            TitleId = rated.Id,
            Value = RatingValues.Like,
            RatedAt = now,
        });
        await db.SaveChangesAsync();

        return new Seed(netflixIt.Id, removed.Id, rated.Id, primeOnly.Id);
    }

    private sealed record Seed(int NetflixItId, int RemovedTitleId, int RatedTitleId, int PrimeOnlyTitleId);
}
