using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Metadata;
using PickArr.Core.Recommendations;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Catalog;
using PickArr.Infrastructure.Metadata.Reco;
using PickArr.Infrastructure.Persistence;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

public sealed class RecoEvalServiceTests : IAsyncLifetime
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
    public async Task Evaluate_with_few_ratings_sets_low_sample_warning_and_keeps_l3_inactive()
    {
        var (db, eval) = await SeedAsync(ratingCount: 12, minRatings: 100);
        var report = await eval.EvaluateAsync();

        report.LowSampleWarning.ShouldBeTrue();
        report.Level3Active.ShouldBeFalse();
        report.Prior.ShouldNotBeNull();
        report.Level1.ShouldNotBeNull();
        report.Combined.ShouldNotBeNull();

        var state = await db.RecoStates.SingleAsync(s => s.Id == RecoState.SingletonId);
        state.LastEvalJson.ShouldNotBeNullOrWhiteSpace();
        state.Level3Active.ShouldBeFalse();

        var latest = await eval.GetLatestAsync();
        latest.ShouldNotBeNull();
        latest!.RatingCount.ShouldBe(12);
    }

    [Fact]
    public async Task Level3_inactive_under_min_ratings_even_when_gate_checked()
    {
        var (_, eval) = await SeedAsync(ratingCount: 40, minRatings: 100);
        var report = await eval.EvaluateAsync();
        report.Level3Active.ShouldBeFalse();
        report.Level3SkipReason.ShouldBe("below_min_ratings");
    }

    private async Task<(PickArrDbContext Db, RecoEvalService Eval)> SeedAsync(int ratingCount, int minRatings)
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickArrDbContext>().UseSqlite(_connection).Options;
        var db = new PickArrDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
        var provider = new Provider
        {
            Key = "netflix",
            DisplayName = "Netflix",
            ProviderType = "watchmode",
            ExternalId = "203",
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
            BaselineCompletedAt = now,
        };
        db.CatalogSources.Add(source);
        await db.SaveChangesAsync();

        for (var i = 0; i < 80; i++)
        {
            db.Titles.Add(new Title
            {
                WatchmodeId = 9_000_000 + i,
                MediaType = i % 2 == 0 ? MediaTypes.Movie : MediaTypes.Series,
                Category = i % 2 == 0 ? TitleCategories.Movie : TitleCategories.Series,
                OriginalTitle = $"Eval {i}",
                OriginalTitleNormalized = $"eval {i}",
                ReleaseYear = 2000 + (i % 20),
                VoteTmdb = 5 + (i % 40) / 10.0,
                VoteTmdbCount = 100 + i,
                Popularity = 1 + i,
                MetadataStatus = MetadataStatuses.Ready,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        for (var g = 1; g <= 5; g++)
        {
            db.Genres.Add(new Genre { Id = g });
        }

        await db.SaveChangesAsync();
        var titles = await db.Titles.OrderBy(t => t.Id).ToListAsync();
        foreach (var t in titles)
        {
            db.Availabilities.Add(new Availability
            {
                TitleId = t.Id,
                CatalogSourceId = source.Id,
                FirstSeenAt = now,
                LastSeenAt = now,
                AddedAt = now,
            });
            db.TitleGenres.Add(new TitleGenre { TitleId = t.Id, GenreId = (t.Id % 5) + 1 });
        }

        for (var i = 0; i < ratingCount; i++)
        {
            db.Ratings.Add(new Rating
            {
                TitleId = titles[i].Id,
                Value = i % 3 == 0 ? RatingValues.Dislike : RatingValues.Like,
                RatedAt = now.AddMinutes(-i),
            });
        }

        db.RecoStates.Add(new RecoState { Id = RecoState.SingletonId });
        await db.SaveChangesAsync();

        var settings = new MemorySettingsStore();
        await settings.SetAsync(SettingsKeys.RecoMlMinRatings, minRatings.ToString(), false);
        await settings.SetAsync(SettingsKeys.RecoEmbeddingsEnabled, "false", false);
        await settings.SetAsync(SettingsKeys.RecoN0, "30", false);

        var eval = new RecoEvalService(db, new FixedClock(now), settings, new RecoLevel3Model());
        return (db, eval);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class MemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<string?> GetRawAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(key, out var v) ? v : null);

        public Task SetAsync(string key, string valueJson, bool isSecret, CancellationToken cancellationToken = default)
        {
            _values[key] = valueJson;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }

        public Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.ContainsKey(key));
    }
}
