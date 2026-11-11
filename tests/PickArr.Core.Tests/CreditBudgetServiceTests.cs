using System.Text.Json;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Catalog;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Settings;
using Shouldly;

namespace Pickarr.Core.Tests;

public sealed class CreditBudgetServiceTests
{
    [Fact]
    public async Task TryReserveAsync_increments_run_credits_when_allowed()
    {
        var run = NewRun();
        var store = new FakeSyncRunStore(run);
        var settings = new FakeSettingsStore();
        await settings.SetAsync(SettingsKeys.WatchmodeQuota, "100", false);
        await settings.SetAsync(SettingsKeys.WatchmodeReservePercent, "10", false);
        await settings.SetAsync(SettingsKeys.WatchmodeMaxCreditsPerRun, "80", false);

        var budget = new CreditBudgetService(
            settings,
            new FakeUsageReader(used: 80, quota: 100),
            store,
            new WatchmodeQuotaObservation());

        (await budget.TryReserveAsync(run.Id, cost: 10, isManual: false)).ShouldBeTrue();
        (await store.GetAsync(run.Id))!.CreditsUsed.ShouldBe(10);

        (await budget.TryReserveAsync(run.Id, cost: 1, isManual: false)).ShouldBeFalse();
        (await store.GetAsync(run.Id))!.CreditsUsed.ShouldBe(10);
    }

    [Fact]
    public async Task TryReserveAsync_manual_can_spend_into_reserve()
    {
        var run = NewRun(triggeredBy: SyncRunTriggers.Manual);
        var store = new FakeSyncRunStore(run);
        var settings = new FakeSettingsStore();
        await settings.SetAsync(SettingsKeys.WatchmodeQuota, "100", false);
        await settings.SetAsync(SettingsKeys.WatchmodeReservePercent, "10", false);
        await settings.SetAsync(SettingsKeys.WatchmodeMaxCreditsPerRun, "80", false);

        var budget = new CreditBudgetService(
            settings,
            new FakeUsageReader(used: 80, quota: 100),
            store,
            new WatchmodeQuotaObservation());

        (await budget.TryReserveAsync(run.Id, cost: 20, isManual: true)).ShouldBeTrue();
        (await budget.TryReserveAsync(run.Id, cost: 1, isManual: true)).ShouldBeFalse();
    }

    [Fact]
    public async Task ObserveQuotaAsync_overrides_reader_used()
    {
        var run = NewRun();
        var store = new FakeSyncRunStore(run);
        var settings = new FakeSettingsStore();
        await settings.SetAsync(SettingsKeys.WatchmodeQuota, "2500", false);
        await settings.SetAsync(SettingsKeys.WatchmodeReservePercent, "10", false);
        await settings.SetAsync(SettingsKeys.WatchmodeMaxCreditsPerRun, "80", false);

        var observation = new WatchmodeQuotaObservation();
        var budget = new CreditBudgetService(
            settings,
            new FakeUsageReader(used: 0, quota: 2500),
            store,
            observation);
        await budget.ObserveQuotaAsync(quotaUsed: 2490, quotaLimit: 2500);

        // spendable scheduled = 2500 - 2490 - 250 = 0
        (await budget.TryReserveAsync(run.Id, cost: 1, isManual: false)).ShouldBeFalse();
        (await budget.TryReserveAsync(run.Id, cost: 10, isManual: true)).ShouldBeTrue();
    }

    [Fact]
    public async Task ObserveQuotaAsync_is_shared_across_budget_instances()
    {
        var run = NewRun();
        var store = new FakeSyncRunStore(run);
        var settings = new FakeSettingsStore();
        await settings.SetAsync(SettingsKeys.WatchmodeQuota, "2500", false);
        await settings.SetAsync(SettingsKeys.WatchmodeReservePercent, "10", false);
        await settings.SetAsync(SettingsKeys.WatchmodeMaxCreditsPerRun, "80", false);

        var observation = new WatchmodeQuotaObservation();
        var writer = new CreditBudgetService(
            settings,
            new FakeUsageReader(used: 0, quota: 2500),
            store,
            observation);
        await writer.ObserveQuotaAsync(quotaUsed: 2490, quotaLimit: 2500);

        var reader = new CreditBudgetService(
            settings,
            new FakeUsageReader(used: 0, quota: 2500),
            store,
            observation);
        (await reader.TryReserveAsync(run.Id, cost: 1, isManual: false)).ShouldBeFalse();
    }

    [Fact]
    public async Task GetSnapshotAsync_exposes_spendable_windows()
    {
        var settings = new FakeSettingsStore();
        await settings.SetAsync(SettingsKeys.WatchmodeQuota, "100", false);
        await settings.SetAsync(SettingsKeys.WatchmodeReservePercent, "10", false);
        await settings.SetAsync(SettingsKeys.WatchmodeMaxCreditsPerRun, "50", false);

        var budget = new CreditBudgetService(
            settings,
            new FakeUsageReader(used: 80, quota: 100),
            new FakeSyncRunStore(NewRun()),
            new WatchmodeQuotaObservation());

        var snap = await budget.GetSnapshotAsync();
        snap.Used.ShouldBe(80);
        snap.Quota.ShouldBe(100);
        snap.Reserve.ShouldBe(10);
        snap.SpendableScheduled.ShouldBe(10);
        snap.SpendableManual.ShouldBe(20);
        snap.MaxCreditsPerRun.ShouldBe(50);
    }

    private static SyncRun NewRun(string triggeredBy = SyncRunTriggers.Schedule) => new()
    {
        Id = Guid.NewGuid(),
        Kind = SyncRunKinds.CatalogIncremental,
        TriggeredBy = triggeredBy,
        StartedAt = DateTime.UtcNow,
        Status = SyncRunStatuses.Running,
    };

    private sealed class FakeUsageReader(int used, int? quota) : IQuotaUsageReader
    {
        public Task<QuotaUsage> GetWatchmodeUsageAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new QuotaUsage(used, quota));
    }

    private sealed class FakeSettingsStore : ISettingsStore
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

    private sealed class FakeSyncRunStore(SyncRun run) : ISyncRunStore
    {
        public Task<SyncRun?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<SyncRun?>(id == run.Id ? run : null);

        public Task<SyncRun> AddAsync(SyncRun entity, CancellationToken cancellationToken = default) =>
            Task.FromResult(entity);

        public Task UpdateAsync(SyncRun entity, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<SyncRun> StartAsync(
            string kind,
            int? catalogSourceId,
            string triggeredBy,
            string? checkpointJson = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task PauseAsync(Guid id, string checkpointJson, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SucceedAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task FailAsync(Guid id, string errorMessage, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task MarkNeedsReviewAsync(Guid id, string? errorMessage = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task PruneHistoryAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
