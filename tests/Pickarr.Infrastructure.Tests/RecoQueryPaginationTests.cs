using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Recommendations;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Catalog;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class RecoQueryPaginationTests : IAsyncLifetime
{
    private SqliteConnection? _connection;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Explore_pages_do_not_repeat_title_ids()
    {
        // Window grows past DefaultCandidateCap (120) on page 3 with pageSize 48.
        // Explore injects titles from beyond that cap into early slots; without a stable
        // prefix+tail those ids reappear when the growing window reaches them.
        const int titleCount = 160;
        const int pageSize = 48;
        var (query, sourceId, profileId) = await SeedAsync(titleCount, exploreRate: 0.2);

        var seen = new HashSet<int>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await query.QueryAsync(new RecommendationsQuery(
                Scope: RecoMembership.ScopeSource,
                SourceId: sourceId,
                Diversify: true,
                Page: page,
                PageSize: pageSize,
                ProfileId: profileId));

            result.Total.ShouldBe(titleCount);
            foreach (var id in result.Items.Select(i => i.Id))
            {
                seen.Add(id).ShouldBeTrue($"title {id} already appeared on an earlier page");
            }
        }

        seen.Count.ShouldBe(pageSize * 3);
    }

    [Fact]
    public async Task Explore_prefix_order_is_stable_across_pages()
    {
        const int titleCount = 160;
        const int pageSize = 48;
        var (query, sourceId, profileId) = await SeedAsync(titleCount, exploreRate: 0.2);

        var page1 = await query.QueryAsync(new RecommendationsQuery(
            Scope: RecoMembership.ScopeSource,
            SourceId: sourceId,
            Diversify: true,
            Page: 1,
            PageSize: pageSize,
            ProfileId: profileId));
        var page3 = await query.QueryAsync(new RecommendationsQuery(
            Scope: RecoMembership.ScopeSource,
            SourceId: sourceId,
            Diversify: true,
            Page: 3,
            PageSize: pageSize,
            ProfileId: profileId));

        // Re-query page 1 after a deeper page: same diversify inputs → same prefix.
        var page1Again = await query.QueryAsync(new RecommendationsQuery(
            Scope: RecoMembership.ScopeSource,
            SourceId: sourceId,
            Diversify: true,
            Page: 1,
            PageSize: pageSize,
            ProfileId: profileId));

        page1Again.Items.Select(i => i.Id).ShouldBe(page1.Items.Select(i => i.Id));
        page3.Items.Select(i => i.Id).ShouldNotContain(id => page1.Items.Any(i => i.Id == id));
    }

    private async Task<(RecoQueryService Query, int SourceId, int ProfileId)> SeedAsync(
        int titleCount,
        double exploreRate)
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var db = new PickarrDbContext(new DbContextOptionsBuilder<PickarrDbContext>().UseSqlite(_connection).Options);
        await db.Database.EnsureCreatedAsync();

        var now = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);
        var provider = new Provider
        {
            Key = "netflix",
            DisplayName = "Netflix",
            ProviderType = "streaming",
            IsEnabled = true,
            CreatedAt = now,
        };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();

        var source = new CatalogSource
        {
            ProviderId = provider.Id,
            CountryCode = "IT",
            MetadataLanguage = "en-US",
            IsEnabled = true,
        };
        db.CatalogSources.Add(source);
        var profile = new Profile
        {
            Name = "Admin",
            Color = Profile.DefaultColor,
            IsAdmin = true,
            CreatedAt = now,
        };
        db.Profiles.Add(profile);

        var titles = Enumerable.Range(1, titleCount)
            .Select(i => new Title
            {
                Id = i,
                MediaType = MediaTypes.Movie,
                Category = TitleCategories.Movie,
                OriginalTitle = $"T{i}",
                MetadataStatus = MetadataStatuses.Ready,
                CreatedAt = now,
                UpdatedAt = now,
            })
            .ToList();
        db.Titles.AddRange(titles);
        await db.SaveChangesAsync();

        db.Availabilities.AddRange(titles.Select(t => new Availability
        {
            TitleId = t.Id,
            CatalogSourceId = source.Id,
            FirstSeenAt = now,
            LastSeenAt = now,
        }));
        db.RecoStates.Add(new RecoState
        {
            ProfileId = profile.Id,
            CurrentVersion = 1,
            Stage = RecoLearning.StageCold,
        });
        // Prior tracks Rank so explore candidates beyond the MMR cap (ranks 121+) are preferred.
        db.Recommendations.AddRange(titles.Select((t, index) => new Recommendation
        {
            ProfileId = profile.Id,
            Version = 1,
            TitleId = t.Id,
            Score = 1.0 - (index / (double)titleCount),
            Prior = 1.0 - (index / (double)titleCount),
            Rank = index + 1,
        }));
        await db.SaveChangesAsync();

        var settings = new MapSettings(new Dictionary<string, string>
        {
            [SettingsKeys.RecoExploreRate] = exploreRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [SettingsKeys.RecoMmrLambda] = "0.7",
            [SettingsKeys.RecoExcludeInLibrary] = "false",
        });
        return (new RecoQueryService(db, settings, new FixedClock(now)), source.Id, profile.Id);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class MapSettings(Dictionary<string, string> values) : ISettingsStore
    {
        public Task<string?> GetRawAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(values.GetValueOrDefault(key));

        public Task SetAsync(string key, string valueJson, bool isSecret, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(values.ContainsKey(key));
    }
}
