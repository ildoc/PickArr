using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Arr;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Recommendations;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Catalog;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class ProfileRecoPreferenceTests : IAsyncLifetime
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
    public async Task For_you_library_filter_follows_the_profile_not_the_global_key()
    {
        var (db, query, sourceId, adminId, otherId) = await SeedAsync();

        var hidden = await query.QueryAsync(new RecommendationsQuery(
            Scope: RecoMembership.ScopeSource,
            SourceId: sourceId,
            Diversify: false,
            ProfileId: adminId));
        var shown = await query.QueryAsync(new RecommendationsQuery(
            Scope: RecoMembership.ScopeSource,
            SourceId: sourceId,
            Diversify: false,
            ProfileId: otherId));

        hidden.Items.Select(item => item.Id).ShouldBe([101]);
        shown.Items.Select(item => item.Id).ShouldBe([101, 102]);
    }

    private async Task<(PickarrDbContext Db, RecoQueryService Query, int SourceId, int AdminId, int OtherId)> SeedAsync()
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
        var admin = new Profile
        {
            Name = "Admin",
            Color = Profile.DefaultColor,
            IsAdmin = true,
            ExcludeInLibrary = true,
            CreatedAt = now,
        };
        var other = new Profile
        {
            Name = "Other",
            Color = "teal",
            ExcludeInLibrary = false,
            CreatedAt = now,
        };
        db.Profiles.AddRange(admin, other);
        db.Titles.AddRange(Title(101), Title(102));
        await db.SaveChangesAsync();

        db.Availabilities.AddRange(
            new Availability { TitleId = 101, CatalogSourceId = source.Id, FirstSeenAt = now, LastSeenAt = now },
            new Availability { TitleId = 102, CatalogSourceId = source.Id, FirstSeenAt = now, LastSeenAt = now });
        db.ArrLibraryItems.Add(new ArrLibraryItem
        {
            Kind = ArrKinds.Radarr,
            ArrId = 102,
            TitleId = 102,
            ArrTitle = "In library",
            State = ArrStates.Downloaded,
            SyncedAt = now,
        });
        foreach (var profileId in new[] { admin.Id, other.Id })
        {
            db.RecoStates.Add(new RecoState
            {
                ProfileId = profileId,
                CurrentVersion = 1,
                Stage = RecoLearning.StageCold,
            });
            db.Recommendations.AddRange(
                new Recommendation { ProfileId = profileId, Version = 1, TitleId = 101, Score = 1, Prior = 0.5, Rank = 1 },
                new Recommendation { ProfileId = profileId, Version = 1, TitleId = 102, Score = 0.5, Prior = 0.4, Rank = 2 });
        }

        await db.SaveChangesAsync();
        var settings = new MapSettings(new Dictionary<string, string>
        {
            [SettingsKeys.RecoExcludeInLibrary] = "false",
            [SettingsKeys.RecoExploreRate] = "0",
        });
        return (db, new RecoQueryService(db, settings, new FixedClock(now)), source.Id, admin.Id, other.Id);
    }

    private static Title Title(int id) => new()
    {
        Id = id,
        MediaType = MediaTypes.Movie,
        Category = TitleCategories.Movie,
        OriginalTitle = $"T{id}",
        MetadataStatus = MetadataStatuses.Ready,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

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
