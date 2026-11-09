using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Catalog;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class RatingServiceTests : IAsyncLifetime
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
    public async Task Set_replaces_value_and_writes_events()
    {
        var (db, ratings, titleId) = await SeedAsync();

        await ratings.SetAsync(titleId, RatingValues.Like, RatingEventOrigins.Ui);
        await ratings.SetAsync(titleId, RatingValues.Dislike, RatingEventOrigins.Ui);

        var rating = await db.Ratings.SingleAsync(r => r.TitleId == titleId);
        rating.Value.ShouldBe(RatingValues.Dislike);
        (await db.RatingEvents.CountAsync(e => e.TitleId == titleId)).ShouldBe(2);
    }

    [Fact]
    public async Task Clear_removes_row_and_logs_null_event()
    {
        var (db, ratings, titleId) = await SeedAsync();
        await ratings.SetAsync(titleId, RatingValues.Like, RatingEventOrigins.Ui);

        await ratings.ClearAsync(titleId, RatingEventOrigins.Ui);

        (await db.Ratings.CountAsync()).ShouldBe(0);
        var cleared = await db.RatingEvents.OrderByDescending(e => e.Id).FirstAsync();
        cleared.Value.ShouldBeNull();
        cleared.Origin.ShouldBe(RatingEventOrigins.Ui);
    }

    [Fact]
    public async Task Set_when_disabled_throws_RecoDisabled()
    {
        var (_, ratings, titleId) = await SeedAsync(enabled: false);

        await Should.ThrowAsync<RecoDisabledException>(() =>
            ratings.SetAsync(titleId, RatingValues.Like, RatingEventOrigins.Ui));
    }

    [Fact]
    public async Task Second_identical_put_is_idempotent_without_extra_event()
    {
        var (db, ratings, titleId) = await SeedAsync();
        await ratings.SetAsync(titleId, RatingValues.Like, RatingEventOrigins.Ui);
        await ratings.SetAsync(titleId, RatingValues.Like, RatingEventOrigins.Ui);

        (await db.RatingEvents.CountAsync(e => e.TitleId == titleId)).ShouldBe(1);
    }

    private async Task<(PickarrDbContext Db, RatingService Ratings, int TitleId)> SeedAsync(bool enabled = true)
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PickarrDbContext>()
            .UseSqlite(_connection)
            .Options;
        var db = new PickarrDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        db.Settings.Add(new AppSetting
        {
            Key = SettingsKeys.RecoEnabled,
            ValueJson = enabled ? "true" : "false",
            IsSecret = false,
            UpdatedAt = now,
        });
        var title = new Title
        {
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = "Rated Movie",
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Titles.Add(title);
        await db.SaveChangesAsync();

        var settings = new MemorySettingsStore(enabled);
        var ratings = new RatingService(db, new FixedClock(now), settings, new NoopRebuildScheduler(), new NoopEventLog());
        return (db, ratings, title.Id);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class NoopRebuildScheduler : IRecoRebuildScheduler
    {
        public void ScheduleDebouncedRebuild() { }
    }

    private sealed class NoopEventLog : IRecoEventLogService
    {
        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task LogAsync(
            int titleId,
            string eventKind,
            int? version = null,
            int? rank = null,
            string? scope = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task LogManyAsync(
            IReadOnlyList<RecoEventLogEntry> entries,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class MemorySettingsStore(bool enabled) : ISettingsStore
    {
        public Task<string?> GetRawAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(key == SettingsKeys.RecoEnabled
                ? (enabled ? "true" : "false")
                : null);

        public Task SetAsync(string key, string valueJson, bool isSecret, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(key == SettingsKeys.RecoEnabled);
    }
}
