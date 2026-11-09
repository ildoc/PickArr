using Hangfire;
using Hangfire.InMemory;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PickArr.Core.Abstractions;
using PickArr.Core.Admin;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Admin;
using PickArr.Infrastructure.Jobs;
using PickArr.Infrastructure.Persistence;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

public sealed class AdminJobServiceTests
{
    [Fact]
    public async Task TriggerAsync_requires_job_key()
    {
        await using var fx = await Fixture.CreateAsync();

        var error = await Should.ThrowAsync<ArgumentException>(() =>
            fx.Sut.TriggerAsync("  ", new TriggerJobRequest(null, null)));

        error.ParamName.ShouldBe("jobKey");
    }

    [Fact]
    public async Task TriggerAsync_catalog_requires_source_id()
    {
        await using var fx = await Fixture.CreateAsync();

        var error = await Should.ThrowAsync<ArgumentException>(() =>
            fx.Sut.TriggerAsync("catalog", new TriggerJobRequest(null, "incremental")));

        error.Message.ShouldContain("sourceId is required");
    }

    [Fact]
    public async Task TriggerAsync_rejects_unsupported_catalog_mode()
    {
        await using var fx = await Fixture.CreateAsync();

        var error = await Should.ThrowAsync<ArgumentException>(() =>
            fx.Sut.TriggerAsync("catalog", new TriggerJobRequest(1, "weekly")));

        error.Message.ShouldContain("Unsupported catalog mode");
    }

    [Fact]
    public async Task TriggerAsync_rejects_unknown_job_key()
    {
        await using var fx = await Fixture.CreateAsync();

        var error = await Should.ThrowAsync<ArgumentException>(() =>
            fx.Sut.TriggerAsync("not-a-real-job", new TriggerJobRequest(null, null)));

        error.Message.ShouldContain("Unknown jobKey");
    }

    [Fact]
    public async Task TriggerAsync_rejects_invalid_incremental_job_key()
    {
        await using var fx = await Fixture.CreateAsync();

        var error = await Should.ThrowAsync<ArgumentException>(() =>
            fx.Sut.TriggerAsync("catalog-incr-abc", new TriggerJobRequest(null, null)));

        error.Message.ShouldContain("Invalid jobKey");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public required AdminJobService Sut { get; init; }
        public required PickArrDbContext Db { get; init; }

        private Fixture(SqliteConnection connection) => _connection = connection;

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PickArrDbContext>().UseSqlite(connection).Options;
            var db = new PickArrDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var settings = Substitute.For<ISettingsStore>();
            var jobs = Substitute.For<IBackgroundJobClient>();
            var storage = new InMemoryStorage();
            JobStorage.Current = storage;
            var reconciler = new ScheduleReconciler(
                db,
                new RecurringJobManager(storage),
                settings,
                NullLogger<ScheduleReconciler>.Instance);
            var bootstrap = new CatalogBootstrap(
                db,
                settings,
                jobs,
                reconciler,
                Options.Create(new CatalogBootstrapOptions { AutoInitialImport = false }),
                NullLogger<CatalogBootstrap>.Instance);
            var sut = new AdminJobService(db, jobs, settings, bootstrap);
            return new Fixture(connection) { Db = db, Sut = sut };
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
