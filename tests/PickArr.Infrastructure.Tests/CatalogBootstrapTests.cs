using Hangfire;
using Hangfire.Common;
using Hangfire.InMemory;
using Hangfire.States;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Jobs;
using PickArr.Infrastructure.Persistence;
using PickArr.Infrastructure.Settings;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

public sealed class CatalogBootstrapTests
{
    [Fact]
    public async Task StartAsync_requeues_until_the_catalog_has_been_imported()
    {
        await using var fixture = await Fixture.CreateAsync(auto: true, watchmodeKey: true);
        foreach (var source in await fixture.Db.CatalogSources
                     .Where(s => s.CountryCode == "IT" || s.CountryCode == "US")
                     .ToListAsync())
        {
            source.IsEnabled = true;
        }

        await fixture.Db.SaveChangesAsync();
        var first = await fixture.Bootstrap.StartAsync(force: false);
        first.ShouldNotBeNull();
        (await fixture.Db.CatalogSources.CountAsync(s => s.IsEnabled)).ShouldBe(2);
        fixture.Jobs.Received(1).Create(
            Arg.Is<Job>(job => job.Type == typeof(CatalogBootstrapJob)),
            Arg.Any<IState>());

        var second = await fixture.Bootstrap.StartAsync(force: false);
        second.ShouldNotBeNull();
        fixture.Jobs.Received(2).Create(
            Arg.Is<Job>(job => job.Type == typeof(CatalogBootstrapJob)),
            Arg.Any<IState>());

        foreach (var source in await fixture.Db.CatalogSources.ToListAsync())
        {
            source.LastIncrementalAt = DateTime.UtcNow;
        }

        await fixture.Db.SaveChangesAsync();
        var third = await fixture.Bootstrap.StartAsync(force: false);
        third.ShouldBeNull();
        fixture.Jobs.Received(2).Create(
            Arg.Is<Job>(job => job.Type == typeof(CatalogBootstrapJob)),
            Arg.Any<IState>());
    }

    [Fact]
    public async Task StartAsync_closes_runs_left_running_by_a_restart()
    {
        await using var fixture = await Fixture.CreateAsync(auto: false, watchmodeKey: true);
        var runId = Guid.NewGuid();
        fixture.Db.SyncRuns.Add(new SyncRun
        {
            Id = runId,
            Kind = SyncRunKinds.CatalogIncremental,
            TriggeredBy = SyncRunTriggers.Schedule,
            StartedAt = DateTime.UtcNow,
            Status = SyncRunStatuses.Running,
        });
        await fixture.Db.SaveChangesAsync();

        var jobId = await fixture.Bootstrap.StartAsync(force: false);
        jobId.ShouldBeNull();

        var run = await fixture.Db.SyncRuns.SingleAsync();
        run.Status.ShouldBe(SyncRunStatuses.Failed);
        run.FinishedAt.ShouldNotBeNull();
        run.ErrorMessage.ShouldBe("Interrupted by an application restart.");
    }

    [Fact]
    public async Task StartAsync_enables_netflix_it_and_us_when_nothing_is_selected()
    {
        await using var fixture = await Fixture.CreateAsync(auto: true, watchmodeKey: true);
        var jobId = await fixture.Bootstrap.StartAsync(force: false);
        jobId.ShouldNotBeNull();

        var enabled = await fixture.Db.CatalogSources
            .Where(s => s.IsEnabled)
            .Select(s => s.CountryCode)
            .Order()
            .ToListAsync();
        enabled.ShouldBe(["IT", "US"]);
    }

    [Fact]
    public async Task StartAsync_does_nothing_until_a_watchmode_key_exists()
    {
        await using var fixture = await Fixture.CreateAsync(auto: true, watchmodeKey: false);
        var jobId = await fixture.Bootstrap.StartAsync(force: false);
        jobId.ShouldBeNull();
        (await fixture.Db.CatalogSources.CountAsync(s => s.IsEnabled)).ShouldBe(0);
        fixture.Jobs.DidNotReceive().Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    [Fact]
    public async Task StartAsync_sees_a_watchmode_key_declared_by_the_environment_store()
    {
        await using var fixture = await Fixture.CreateAsync(auto: true, watchmodeKey: false);
        var source = CreateEnvSource(new Dictionary<string, string?>
        {
            ["PickArr:Settings:watchmode:apiKey"] = "from-env",
        });
        var bootstrap = new CatalogBootstrap(
            fixture.Db,
            new EnvironmentSettingsStore(fixture.Settings, source),
            fixture.Jobs,
            fixture.Reconciler,
            Options.Create(new CatalogBootstrapOptions { AutoInitialImport = true }),
            NullLogger<CatalogBootstrap>.Instance);

        var jobId = await bootstrap.StartAsync(force: false);

        jobId.ShouldNotBeNull();
        (await fixture.Db.CatalogSources.CountAsync(s => s.IsEnabled)).ShouldBe(2);
        fixture.Jobs.Received().Create(
            Arg.Is<Job>(job => job.Type == typeof(CatalogBootstrapJob)),
            Arg.Any<IState>());
        fixture.Jobs.Received().Create(
            Arg.Is<Job>(job => job.Type == typeof(CatalogIncrementalSyncJob)),
            Arg.Any<IState>());
    }

    [Fact]
    public async Task StartAsync_can_be_turned_off()
    {
        await using var fixture = await Fixture.CreateAsync(auto: false, watchmodeKey: true);
        var jobId = await fixture.Bootstrap.StartAsync(force: false);
        jobId.ShouldBeNull();
        fixture.Jobs.DidNotReceive().Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    [Fact]
    public async Task ImportAsync_queues_a_first_page_import_per_country()
    {
        await using var fixture = await Fixture.CreateAsync(auto: true, watchmodeKey: true);
        foreach (var source in await fixture.Db.CatalogSources
                     .Where(s => s.CountryCode == "IT" || s.CountryCode == "US")
                     .ToListAsync())
        {
            source.IsEnabled = true;
        }

        await fixture.Db.SaveChangesAsync();
        await fixture.Bootstrap.ImportAsync();

        (await fixture.Db.CatalogSources.CountAsync(s => s.IsEnabled)).ShouldBe(2);
        fixture.Jobs.Received(2).Create(
            Arg.Is<Job>(job => job.Type == typeof(CatalogIncrementalSyncJob)),
            Arg.Any<IState>());
    }

    [Fact]
    public async Task ImportAsync_skips_countries_that_were_already_imported()
    {
        await using var fixture = await Fixture.CreateAsync(auto: true, watchmodeKey: true);
        var selected = await fixture.Db.CatalogSources
            .Where(s => s.CountryCode == "IT" || s.CountryCode == "US")
            .ToListAsync();
        foreach (var source in selected)
        {
            source.IsEnabled = true;
        }

        selected.Single(s => s.CountryCode == "IT").LastIncrementalAt = DateTime.UtcNow;
        await fixture.Db.SaveChangesAsync();

        await fixture.Bootstrap.ImportAsync();

        fixture.Jobs.Received(1).Create(
            Arg.Is<Job>(job => job.Type == typeof(CatalogIncrementalSyncJob)),
            Arg.Any<IState>());
    }

    [Fact]
    public async Task ImportAsync_leaves_disabled_countries_alone()
    {
        await using var fixture = await Fixture.CreateAsync(auto: true, watchmodeKey: true);
        var italy = await fixture.Db.CatalogSources.SingleAsync(s => s.CountryCode == "IT");
        italy.IsEnabled = true;
        await fixture.Db.SaveChangesAsync();

        await fixture.Bootstrap.ImportAsync();

        (await fixture.Db.CatalogSources.CountAsync(s => s.IsEnabled)).ShouldBe(1);
        fixture.Jobs.Received(1).Create(
            Arg.Is<Job>(job => job.Type == typeof(CatalogIncrementalSyncJob)),
            Arg.Any<IState>());
    }

    private static EnvironmentSettingSource CreateEnvSource(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new EnvironmentSettingSource(configuration, NullLogger<EnvironmentSettingSource>.Instance);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public required PickArrDbContext Db { get; init; }
        public required CatalogBootstrap Bootstrap { get; init; }
        public required IBackgroundJobClient Jobs { get; init; }
        public required MemorySettings Settings { get; init; }
        public required ScheduleReconciler Reconciler { get; init; }

        private Fixture(SqliteConnection connection) => _connection = connection;

        public static async Task<Fixture> CreateAsync(bool auto, bool watchmodeKey)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PickArrDbContext>().UseSqlite(connection).Options;
            var db = new PickArrDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var provider = new Provider
            {
                Key = "netflix",
                DisplayName = "Netflix",
                ProviderType = "watchmode",
                ExternalId = "203",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
            };
            db.Providers.Add(provider);
            await db.SaveChangesAsync();
            db.CatalogSources.AddRange(
                new CatalogSource
                {
                    ProviderId = provider.Id,
                    CountryCode = "IT",
                    MetadataLanguage = "it-IT",
                    IsEnabled = false,
                },
                new CatalogSource
                {
                    ProviderId = provider.Id,
                    CountryCode = "US",
                    MetadataLanguage = "en-US",
                    IsEnabled = false,
                });
            await db.SaveChangesAsync();

            var storage = new InMemoryStorage();
            JobStorage.Current = storage;
            var settings = new MemorySettings();
            if (watchmodeKey)
            {
                await settings.SetAsync(SettingsKeys.WatchmodeApiKey, "\"test-key\"", true);
            }

            var jobs = Substitute.For<IBackgroundJobClient>();
            jobs.Create(Arg.Any<Job>(), Arg.Any<IState>()).Returns("hf-1");
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
                Options.Create(new CatalogBootstrapOptions { AutoInitialImport = auto }),
                NullLogger<CatalogBootstrap>.Instance);

            return new Fixture(connection)
            {
                Db = db,
                Bootstrap = bootstrap,
                Jobs = jobs,
                Settings = settings,
                Reconciler = reconciler,
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class MemorySettings : ISettingsStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<string?> GetRawAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

        public Task SetAsync(string key, string valueJson, bool isSecret, CancellationToken cancellationToken = default)
        {
            _values[key] = valueJson;
            return Task.CompletedTask;
        }

        public Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.ContainsKey(key));

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }
}

