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

public sealed class SyncRunStoreTests
{
    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    [Fact]
    public async Task Start_Pause_preserves_checkpoint_for_resume()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickarrDbContext>().UseSqlite(connection).Options;
        await using var db = new PickarrDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var clock = new FixedClock(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc));
        var store = new EfSyncRunStore(db, clock, new TestSettingsStore());

        var run = await store.StartAsync(
            SyncRunKinds.CatalogFull,
            catalogSourceId: null,
            SyncRunTriggers.Schedule);

        run.Status.ShouldBe(SyncRunStatuses.Running);

        await store.PauseAsync(run.Id, """{"page":7}""");
        var paused = await store.GetAsync(run.Id);
        paused.ShouldNotBeNull();
        paused!.Status.ShouldBe(SyncRunStatuses.Paused);
        paused.CheckpointJson.ShouldBe("""{"page":7}""");
        paused.FinishedAt.ShouldBe(clock.UtcNow);

        var resumed = await store.StartAsync(
            SyncRunKinds.CatalogFull,
            catalogSourceId: null,
            SyncRunTriggers.Resume,
            checkpointJson: paused.CheckpointJson);
        resumed.TriggeredBy.ShouldBe(SyncRunTriggers.Resume);
        resumed.CheckpointJson.ShouldBe("""{"page":7}""");
        resumed.Status.ShouldBe(SyncRunStatuses.Running);
    }

    [Fact]
    public async Task FailAsync_discards_a_title_insert_rejected_inside_a_rolled_back_transaction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickarrDbContext>().UseSqlite(connection).Options;
        await using var db = new PickarrDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var clock = new FixedClock(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc));
        var store = new EfSyncRunStore(db, clock, new TestSettingsStore());
        var run = await store.StartAsync(SyncRunKinds.CatalogIncremental, null, SyncRunTriggers.Schedule);

        await using var tx = await db.Database.BeginTransactionAsync();
        db.Titles.Add(SampleTitle(1));
        await db.SaveChangesAsync();
        db.Titles.Add(SampleTitle(2));
        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
        await tx.RollbackAsync();

        await store.FailAsync(run.Id, "UNIQUE constraint failed: Titles.TmdbType, Titles.TmdbId");

        (await db.SyncRuns.SingleAsync()).Status.ShouldBe(SyncRunStatuses.Failed);
        (await db.Titles.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SucceedAsync_keeps_only_configured_history_per_job()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickarrDbContext>().UseSqlite(connection).Options;
        await using var db = new PickarrDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var settings = new TestSettingsStore();
        settings.SetRaw(SettingsKeys.JobsRunHistoryLimit, "2");
        var clock = new FixedClock(new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc));
        var store = new EfSyncRunStore(db, clock, settings);

        for (var i = 0; i < 3; i++)
        {
            var run = await store.StartAsync(SyncRunKinds.Enrichment, null, SyncRunTriggers.Schedule);
            await store.SucceedAsync(run.Id);
            clock = new FixedClock(clock.UtcNow.AddMinutes(1));
            store = new EfSyncRunStore(db, clock, settings);
        }

        var kept = await db.SyncRuns.OrderBy(r => r.StartedAt).ToListAsync();
        kept.Count.ShouldBe(2);
        kept.ShouldAllBe(r => r.Status == SyncRunStatuses.Succeeded);
    }

    private static Title SampleTitle(int watchmodeId) => new()
    {
        WatchmodeId = watchmodeId,
        TmdbId = 555,
        TmdbType = "movie",
        MediaType = "movie",
        Category = "movie",
        MetadataStatus = "pending",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
}
