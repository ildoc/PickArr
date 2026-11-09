using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Infrastructure.Catalog;
using Pickarr.Infrastructure.Jobs;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class MetadataJobRunTests
{
    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class ScriptedEnrichment(params EnrichmentBatchResult[] batches) : IEnrichmentService
    {
        private int _index;

        public Task EnrichTitleAsync(int titleId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<EnrichmentBatchResult> EnrichPendingBatchAsync(
            int limit = 100,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Next());

        public Task<EnrichmentBatchResult> RefreshStaleBatchAsync(
            int limit = 50,
            int olderThanDays = 30,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Next());

        public Task<EnrichmentBatchResult> EnrichFeatureBackfillBatchAsync(
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Next());

        private EnrichmentBatchResult Next()
        {
            if (_index >= batches.Length)
            {
                return new EnrichmentBatchResult(0, 0, 0, 0);
            }

            return batches[_index++];
        }
    }

    [Fact]
    public async Task Enrichment_records_a_succeeded_run_and_the_updated_count()
    {
        await using var db = await CreateDbAsync();
        var store = new EfSyncRunStore(db, new FixedClock(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc)));
        var job = new EnrichmentJob(
            new ScriptedEnrichment(new EnrichmentBatchResult(2, 2, 0, 0), new EnrichmentBatchResult(0, 0, 0, 0)),
            store,
            NullLogger<EnrichmentJob>.Instance);

        await job.ExecuteAsync(isManual: true);

        var run = await db.SyncRuns.SingleAsync();
        run.Kind.ShouldBe(SyncRunKinds.Enrichment);
        run.Status.ShouldBe(SyncRunStatuses.Succeeded);
        run.TriggeredBy.ShouldBe(SyncRunTriggers.Manual);
        run.TitlesUpdated.ShouldBe(2);
        run.PagesFetched.ShouldBe(2);
    }

    [Fact]
    public async Task Enrichment_marks_the_run_failed_when_tmdb_rejects_every_title()
    {
        await using var db = await CreateDbAsync();
        var store = new EfSyncRunStore(db, new FixedClock(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc)));
        var job = new EnrichmentJob(
            new ScriptedEnrichment(new EnrichmentBatchResult(3, 0, 3, 0, "TMDB request failed (401).")),
            store,
            NullLogger<EnrichmentJob>.Instance);

        await job.ExecuteAsync(isManual: true);

        var run = await db.SyncRuns.SingleAsync();
        run.Kind.ShouldBe(SyncRunKinds.Enrichment);
        run.Status.ShouldBe(SyncRunStatuses.Failed);
        run.ErrorMessage.ShouldNotBeNull().ShouldContain("401");
    }

    [Fact]
    public async Task Enrichment_succeeds_when_titles_are_only_unmapped_not_auth_errors()
    {
        await using var db = await CreateDbAsync();
        var store = new EfSyncRunStore(db, new FixedClock(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc)));
        var job = new EnrichmentJob(
            new ScriptedEnrichment(
                new EnrichmentBatchResult(3, 0, 0, 3),
                new EnrichmentBatchResult(2, 2, 0, 0),
                new EnrichmentBatchResult(0, 0, 0, 0)),
            store,
            NullLogger<EnrichmentJob>.Instance);

        await job.ExecuteAsync(isManual: true);

        var run = await db.SyncRuns.SingleAsync();
        run.Status.ShouldBe(SyncRunStatuses.Succeeded);
        run.TitlesUpdated.ShouldBe(2);
        run.PagesFetched.ShouldBe(5);
        run.ErrorMessage.ShouldBeNull();
    }

    private static async Task<PickarrDbContext> CreateDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickarrDbContext>().UseSqlite(connection).Options;
        var db = new PickarrDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}
