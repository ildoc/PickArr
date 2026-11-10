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
        var (db, ratings, profileId, titleId) = await SeedAsync();

        await ratings.SetAsync(profileId, titleId, RatingValues.Like, RatingEventOrigins.Ui);
        await ratings.SetAsync(profileId, titleId, RatingValues.Dislike, RatingEventOrigins.Ui);

        var rating = await db.Ratings.SingleAsync(r => r.TitleId == titleId);
        rating.Value.ShouldBe(RatingValues.Dislike);
        (await db.RatingEvents.CountAsync(e => e.TitleId == titleId)).ShouldBe(2);
    }

    [Fact]
    public async Task Clear_removes_row_and_logs_null_event()
    {
        var (db, ratings, profileId, titleId) = await SeedAsync();
        await ratings.SetAsync(profileId, titleId, RatingValues.Like, RatingEventOrigins.Ui);

        await ratings.ClearAsync(profileId, titleId, RatingEventOrigins.Ui);

        (await db.Ratings.CountAsync()).ShouldBe(0);
        var cleared = await db.RatingEvents.OrderByDescending(e => e.Id).FirstAsync();
        cleared.Value.ShouldBeNull();
        cleared.Origin.ShouldBe(RatingEventOrigins.Ui);
    }

    [Fact]
    public async Task Set_when_disabled_throws_RecoDisabled()
    {
        var (_, ratings, profileId, titleId) = await SeedAsync(enabled: false);

        await Should.ThrowAsync<RecoDisabledException>(() =>
            ratings.SetAsync(profileId, titleId, RatingValues.Like, RatingEventOrigins.Ui));
    }

    [Fact]
    public async Task Second_identical_put_is_idempotent_without_extra_event()
    {
        var (db, ratings, profileId, titleId) = await SeedAsync();
        await ratings.SetAsync(profileId, titleId, RatingValues.Like, RatingEventOrigins.Ui);
        await ratings.SetAsync(profileId, titleId, RatingValues.Like, RatingEventOrigins.Ui);

        (await db.RatingEvents.CountAsync(e => e.TitleId == titleId)).ShouldBe(1);
    }

    [Fact]
    public async Task Set_and_clear_do_not_change_another_profile()
    {
        var (db, ratings, profileId, titleId) = await SeedAsync();
        var other = new Profile
        {
            Name = "Other",
            Color = Profile.DefaultColor,
            CreatedAt = DateTime.UtcNow,
        };
        db.Profiles.Add(other);
        await db.SaveChangesAsync();

        await ratings.SetAsync(profileId, titleId, RatingValues.Like, RatingEventOrigins.Ui);
        await ratings.SetAsync(other.Id, titleId, RatingValues.Dislike, RatingEventOrigins.Ui);
        await ratings.ClearAsync(profileId, titleId, RatingEventOrigins.Ui);

        var remaining = await db.Ratings.SingleAsync();
        remaining.ProfileId.ShouldBe(other.Id);
        remaining.Value.ShouldBe(RatingValues.Dislike);
        (await db.RatingEvents.CountAsync(e => e.ProfileId == profileId)).ShouldBe(2);
        (await db.RatingEvents.CountAsync(e => e.ProfileId == other.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task Set_rebuilds_the_voter_and_profiles_still_under_n0()
    {
        var scheduler = new RecordingScheduler();
        var (db, ratings, profileId, titleId) = await SeedAsync(scheduler: scheduler);
        var now = DateTime.UtcNow;
        var cold = new Profile { Name = "Cold", Color = Profile.DefaultColor, CreatedAt = now };
        var warm = new Profile { Name = "Warm", Color = Profile.DefaultColor, CreatedAt = now };
        var unseen = new Profile { Name = "New", Color = Profile.DefaultColor, CreatedAt = now };
        db.Profiles.AddRange(cold, warm, unseen);
        await db.SaveChangesAsync();
        db.RecoStates.AddRange(
            new RecoState { ProfileId = cold.Id, EffectiveN = 4, Stage = RecoLearning.StageCold },
            new RecoState
            {
                ProfileId = warm.Id,
                EffectiveN = RecoLearning.DefaultN0,
                Stage = RecoLearning.StagePersonalized,
            });
        await db.SaveChangesAsync();

        await ratings.SetAsync(profileId, titleId, RatingValues.Love, RatingEventOrigins.Ui);

        scheduler.ProfileIds.ShouldBe([profileId, cold.Id, unseen.Id]);
    }

    private async Task<(PickarrDbContext Db, RatingService Ratings, int ProfileId, int TitleId)> SeedAsync(
        bool enabled = true,
        IRecoRebuildScheduler? scheduler = null)
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
        var profile = new Profile
        {
            Name = "Rater",
            Color = Profile.DefaultColor,
            CreatedAt = now,
        };
        db.Profiles.Add(profile);
        db.Titles.Add(title);
        await db.SaveChangesAsync();

        var settings = new MemorySettingsStore(enabled);
        var ratings = new RatingService(
            db,
            new FixedClock(now),
            settings,
            scheduler ?? new NoopRebuildScheduler(),
            new NoopEventLog());
        return (db, ratings, profile.Id, title.Id);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class RecordingScheduler : IRecoRebuildScheduler
    {
        public List<int> ProfileIds { get; } = [];

        public void ScheduleDebouncedRebuild(int profileId) => ProfileIds.Add(profileId);
    }

    private sealed class NoopRebuildScheduler : IRecoRebuildScheduler
    {
        public void ScheduleDebouncedRebuild(int profileId) { }
    }

    private sealed class NoopEventLog : IRecoEventLogService
    {
        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task LogAsync(
            int profileId,
            int titleId,
            string eventKind,
            int? version = null,
            int? rank = null,
            string? scope = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task LogManyAsync(
            int profileId,
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
