using Hangfire;
using Hangfire.Common;
using Hangfire.InMemory;
using Hangfire.States;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PickArr.Core.Abstractions;
using PickArr.Core.Catalog;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Catalog;
using PickArr.Infrastructure.Jobs;
using PickArr.Infrastructure.Persistence;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

public sealed class CatalogConfigurationServiceTests
{
    [Fact]
    public async Task GetAsync_exposes_quota_plan_and_provider_catalog()
    {
        await using var fx = await Fixture.CreateAsync();
        var dto = await fx.Sut.GetAsync();

        dto.Plan.ShouldBe("free");
        dto.Quota.ShouldBe(2500);
        dto.Used.ShouldBe(10);
        dto.FreeCountryLimit.ShouldBe(CatalogOffers.FreeCountryLimit);
        dto.Providers.ShouldContain(p => p.Key == "netflix" && p.Enabled);
        dto.Providers.First(p => p.Key == "netflix").Countries
            .ShouldContain(c => c.Code == "IT" && !c.Enabled);
    }

    [Fact]
    public async Task UpdateAsync_persists_enabled_countries()
    {
        await using var fx = await Fixture.CreateAsync();
        var netflix = fx.Db.Providers.Single(p => p.Key == "netflix");

        var dto = await fx.Sut.UpdateAsync(new UpdateCatalogSetupRequest(
        [
            new UpdateCatalogProviderRequest(netflix.Id, Enabled: true, Countries: ["it", "US"]),
        ]));

        var sources = await fx.Db.CatalogSources
            .Where(s => s.ProviderId == netflix.Id)
            .ToListAsync();
        sources.Where(s => s.IsEnabled).Select(s => s.CountryCode).OrderBy(c => c)
            .ShouldBe(["IT", "US"]);
        dto.Providers.First(p => p.Id == netflix.Id).Countries
            .Where(c => c.Enabled).Select(c => c.Code).OrderBy(c => c)
            .ShouldBe(["IT", "US"]);
    }

    [Fact]
    public async Task UpdateAsync_rejects_unknown_provider()
    {
        await using var fx = await Fixture.CreateAsync();

        var error = await Should.ThrowAsync<CatalogSetupException>(() =>
            fx.Sut.UpdateAsync(new UpdateCatalogSetupRequest(
            [
                new UpdateCatalogProviderRequest(999_999, Enabled: true, Countries: ["IT"]),
            ])));

        error.Message.ShouldContain("Unknown provider");
    }

    [Fact]
    public async Task UpdateAsync_rejects_country_not_offered()
    {
        await using var fx = await Fixture.CreateAsync();
        var netflix = fx.Db.Providers.Single(p => p.Key == "netflix");

        var error = await Should.ThrowAsync<CatalogSetupException>(() =>
            fx.Sut.UpdateAsync(new UpdateCatalogSetupRequest(
            [
                new UpdateCatalogProviderRequest(netflix.Id, Enabled: true, Countries: ["ZZ"]),
            ])));

        error.Message.ShouldContain("does not offer ZZ");
    }

    [Fact]
    public async Task UpdateAsync_rejects_enabled_provider_without_countries()
    {
        await using var fx = await Fixture.CreateAsync();
        var netflix = fx.Db.Providers.Single(p => p.Key == "netflix");

        var error = await Should.ThrowAsync<CatalogSetupException>(() =>
            fx.Sut.UpdateAsync(new UpdateCatalogSetupRequest(
            [
                new UpdateCatalogProviderRequest(netflix.Id, Enabled: true, Countries: []),
            ])));

        error.Message.ShouldContain("needs at least one country");
    }

    [Fact]
    public async Task UpdateAsync_rejects_free_plan_over_country_limit()
    {
        await using var fx = await Fixture.CreateAsync(plan: "free");
        var netflix = fx.Db.Providers.Single(p => p.Key == "netflix");

        var error = await Should.ThrowAsync<CatalogSetupException>(() =>
            fx.Sut.UpdateAsync(new UpdateCatalogSetupRequest(
            [
                new UpdateCatalogProviderRequest(
                    netflix.Id,
                    Enabled: true,
                    Countries: ["US", "IT", "GB", "CA"]),
            ])));

        error.Message.ShouldContain($"{CatalogOffers.FreeCountryLimit} countries");
        error.Message.ShouldContain("4 are selected");
    }

    [Fact]
    public async Task UpdateAsync_allows_more_countries_on_paid_plan()
    {
        await using var fx = await Fixture.CreateAsync(plan: "paid", quota: 100_000, used: 0);
        var netflix = fx.Db.Providers.Single(p => p.Key == "netflix");

        var dto = await fx.Sut.UpdateAsync(new UpdateCatalogSetupRequest(
        [
            new UpdateCatalogProviderRequest(
                netflix.Id,
                Enabled: true,
                Countries: ["US", "IT", "GB", "CA"]),
        ]));

        dto.Plan.ShouldBe("paid");
        dto.Providers.First(p => p.Id == netflix.Id).Countries.Count(c => c.Enabled).ShouldBe(4);
    }

    [Fact]
    public async Task UpdateAsync_rejects_selection_that_exceeds_quota()
    {
        await using var fx = await Fixture.CreateAsync(plan: "free", quota: 20, used: 15);
        var netflix = fx.Db.Providers.Single(p => p.Key == "netflix");

        var error = await Should.ThrowAsync<CatalogSetupException>(() =>
            fx.Sut.UpdateAsync(new UpdateCatalogSetupRequest(
            [
                new UpdateCatalogProviderRequest(netflix.Id, Enabled: true, Countries: ["IT"]),
            ])));

        error.Message.ShouldContain("credits");
        error.Message.ShouldContain("15 of 20");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public required PickArrDbContext Db { get; init; }
        public required CatalogConfigurationService Sut { get; init; }

        private Fixture(SqliteConnection connection) => _connection = connection;

        public static async Task<Fixture> CreateAsync(
            string plan = "free",
            int quota = 2500,
            int used = 10)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PickArrDbContext>().UseSqlite(connection).Options;
            var db = new PickArrDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var netflix = new Provider
            {
                Key = "netflix",
                DisplayName = "Netflix",
                ProviderType = "watchmode",
                ExternalId = "203",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
            };
            db.Providers.Add(netflix);
            await db.SaveChangesAsync();
            db.CatalogSources.AddRange(
                new CatalogSource
                {
                    ProviderId = netflix.Id,
                    CountryCode = "IT",
                    MetadataLanguage = "it-IT",
                    IsEnabled = false,
                },
                new CatalogSource
                {
                    ProviderId = netflix.Id,
                    CountryCode = "US",
                    MetadataLanguage = "en-US",
                    IsEnabled = false,
                });
            await db.SaveChangesAsync();

            var storage = new InMemoryStorage();
            JobStorage.Current = storage;
            var settings = new MemorySettings();
            await settings.SetAsync(SettingsKeys.WatchmodePlan, $"\"{plan}\"", false);

            var budget = Substitute.For<ICreditBudget>();
            budget.GetSnapshotAsync(Arg.Any<CancellationToken>())
                .Returns(new CreditBudgetSnapshot(used, quota, Reserve: 0, SpendableScheduled: quota - used, SpendableManual: quota - used, MaxCreditsPerRun: 80));

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
                Options.Create(new CatalogBootstrapOptions { AutoInitialImport = false }),
                NullLogger<CatalogBootstrap>.Instance);

            var sut = new CatalogConfigurationService(db, budget, settings, reconciler, bootstrap);
            return new Fixture(connection) { Db = db, Sut = sut };
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
