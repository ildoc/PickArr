using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Infrastructure.Catalog;
using PickArr.Infrastructure.Persistence;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

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
        var options = new DbContextOptionsBuilder<PickArrDbContext>().UseSqlite(connection).Options;
        await using var db = new PickArrDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var clock = new FixedClock(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc));
        var store = new EfSyncRunStore(db, clock);

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
        var options = new DbContextOptionsBuilder<PickArrDbContext>().UseSqlite(connection).Options;
        await using var db = new PickArrDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var clock = new FixedClock(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc));
        var store = new EfSyncRunStore(db, clock);
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
