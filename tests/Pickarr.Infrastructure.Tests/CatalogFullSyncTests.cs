using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Catalog;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Catalog;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class CatalogFullSyncTests
{
    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;

        public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
    }

    [Fact]
    public async Task Full_sync_sets_baseline_and_does_not_remove_on_first_miss()
    {
        await using var harness = await Harness.CreateAsync(
            pageEntries:
            [
                new CatalogEntry("1", "A", 2020, CatalogEntryType.Movie, 1, "movie", null),
                new CatalogEntry("2", "B", 2021, CatalogEntryType.Movie, 2, "movie", null),
            ],
            maxRemovalRatio: 0.6);

        var result = await harness.FullSync.RunAsync(harness.SourceId);
        result.Status.ShouldBe(SyncRunStatuses.Succeeded);

        var source = await harness.Db.CatalogSources.SingleAsync();
        source.BaselineCompletedAt.ShouldNotBeNull();
        source.LastFullTotalPages.ShouldBe(1);

        (await harness.Db.Availabilities.CountAsync(a => a.IsBaseline)).ShouldBe(2);
        (await harness.Db.AvailabilityEvents.CountAsync()).ShouldBe(0);

        // Second full sync without title 2 → MissedFullScans=1, not removed yet (confirmations=2)
        harness.Clock.Advance(TimeSpan.FromHours(1));
        harness.Provider.Pages =
        [
            [new CatalogEntry("1", "A", 2020, CatalogEntryType.Movie, 1, "movie", null)],
        ];
        var second = await harness.FullSync.RunAsync(harness.SourceId);
        second.Status.ShouldBe(SyncRunStatuses.Succeeded);

        var missed = await harness.Db.Availabilities.Include(a => a.Title)
            .SingleAsync(a => a.Title!.WatchmodeId == 2);
        missed.MissedFullScans.ShouldBe(1);
        missed.RemovedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Full_sync_removes_after_confirmations_and_guards_high_ratio()
    {
        await using var harness = await Harness.CreateAsync(
            pageEntries:
            [
                new CatalogEntry("1", "A", 2020, CatalogEntryType.Movie, 1, "movie", null),
                new CatalogEntry("2", "B", 2021, CatalogEntryType.Movie, 2, "movie", null),
                new CatalogEntry("3", "C", 2022, CatalogEntryType.Movie, 3, "movie", null),
            ],
            maxRemovalRatio: 0.5);

        await harness.FullSync.RunAsync(harness.SourceId);

        // Drop 1 of 3 (ratio 0.33 < 0.5) twice → remove
        harness.Provider.Pages =
        [
            [
                new CatalogEntry("1", "A", 2020, CatalogEntryType.Movie, 1, "movie", null),
                new CatalogEntry("2", "B", 2021, CatalogEntryType.Movie, 2, "movie", null),
            ],
        ];
        harness.Clock.Advance(TimeSpan.FromHours(1));
        await harness.FullSync.RunAsync(harness.SourceId);
        harness.Clock.Advance(TimeSpan.FromHours(1));
        await harness.FullSync.RunAsync(harness.SourceId);

        var removed = await harness.Db.Availabilities.Include(a => a.Title)
            .SingleAsync(a => a.Title!.WatchmodeId == 3);
        removed.RemovedAt.ShouldNotBeNull();
        (await harness.Db.AvailabilityEvents.CountAsync(e => e.Type == AvailabilityEventTypes.Removed)).ShouldBe(1);

        // Both remaining actives missing → ratio 1.0 > 0.5 → NeedsReview, no further removals
        harness.Clock.Advance(TimeSpan.FromHours(1));
        harness.Provider.Pages = [[]];
        var guarded = await harness.FullSync.RunAsync(harness.SourceId);
        guarded.Status.ShouldBe(SyncRunStatuses.NeedsReview);
        (await harness.Db.Availabilities.CountAsync(a => a.RemovedAt == null)).ShouldBe(2);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public required PickarrDbContext Db { get; init; }
        public required ICatalogFullSync FullSync { get; init; }
        public required FakeCatalogProvider Provider { get; init; }
        public required MutableClock Clock { get; init; }
        public required int SourceId { get; init; }

        private Harness(SqliteConnection connection) => _connection = connection;

        public static async Task<Harness> CreateAsync(
            IReadOnlyList<CatalogEntry> pageEntries,
            double maxRemovalRatio = 0.10)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PickarrDbContext>().UseSqlite(connection).Options;
            var db = new PickarrDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var providerEntity = new Provider
            {
                Key = "netflix",
                DisplayName = "Netflix",
                ProviderType = "watchmode",
                ExternalId = "203",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
            };
            db.Providers.Add(providerEntity);
            await db.SaveChangesAsync();
            var source = new CatalogSource
            {
                ProviderId = providerEntity.Id,
                CountryCode = "US",
                MetadataLanguage = "en-US",
                IsEnabled = true,
            };
            db.CatalogSources.Add(source);
            await db.SaveChangesAsync();

            var fakeProvider = new FakeCatalogProvider { Pages = [pageEntries.ToList()] };
            var clock = new MutableClock(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc));
            var settings = new InMemorySettings();
            await settings.SetAsync(SettingsKeys.WatchmodeQuota, "2500", false);
            await settings.SetAsync(SettingsKeys.WatchmodeReservePercent, "10", false);
            await settings.SetAsync(SettingsKeys.WatchmodeMaxCreditsPerRun, "80", false);
            await settings.SetAsync(SettingsKeys.CatalogMaxRemovalRatio, maxRemovalRatio.ToString(System.Globalization.CultureInfo.InvariantCulture), false);
            await settings.SetAsync(SettingsKeys.CatalogRemovalConfirmations, "2", false);

            var syncRuns = new EfSyncRunStore(db, clock, new TestSettingsStore());
            var usage = new FixedUsageReader();
            var budget = new CreditBudgetService(settings, usage, syncRuns, new WatchmodeQuotaObservation());
            var upserter = new CatalogUpserter(db, clock);
            var resolver = new ProviderResolver([fakeProvider]);
            var full = new CatalogFullSync(
                db,
                resolver,
                budget,
                syncRuns,
                upserter,
                settings,
                new ApiUsageSyncRunAccessor(),
                clock,
                NullLogger<CatalogFullSync>.Instance);

            return new Harness(connection)
            {
                Db = db,
                FullSync = full,
                Provider = fakeProvider,
                Clock = clock,
                SourceId = source.Id,
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FakeCatalogProvider : ICatalogProvider
    {
        public List<List<CatalogEntry>> Pages { get; set; } = [];
        public string Type => "watchmode";
        public CatalogProviderCapabilities Capabilities => new(false, true, 250);

        public Task<IReadOnlyList<ProviderInfo>> GetAvailableProvidersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProviderInfo>>([]);

        public Task<IReadOnlyList<string>> GetCountriesAsync(string providerExternalId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(["US"]);

        public Task<CatalogPage> ListTitlesAsync(CatalogListRequest request, CancellationToken cancellationToken = default)
        {
            var index = Math.Max(0, request.Page - 1);
            var entries = index < Pages.Count ? Pages[index] : [];
            return Task.FromResult(new CatalogPage(entries, request.Page, TotalPages: Math.Max(1, Pages.Count), CreditsUsed: 1, TotalResults: entries.Count));
        }

        public Task<IReadOnlyList<LeavingEntry>> GetLeavingAsync(CatalogSourceRef source, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LeavingEntry>>([]);

        public Task<IReadOnlyList<CatalogEntry>> GetChangedTitlesAsync(
            CatalogSourceRef source,
            DateOnly from,
            DateOnly to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogEntry>>([]);
    }

    private sealed class FixedUsageReader : IQuotaUsageReader
    {
        public Task<QuotaUsage> GetWatchmodeUsageAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new QuotaUsage(0, 2500));
    }

    private sealed class InMemorySettings : ISettingsStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<string?> GetRawAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(key, out var v) ? v : null);

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
