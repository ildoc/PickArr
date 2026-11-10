using System.Text.Json;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Settings;

namespace Pickarr.Core.Catalog;

public sealed class CreditBudgetService(
    ISettingsStore settingsStore,
    IQuotaUsageReader usageReader,
    ISyncRunStore syncRunStore,
    WatchmodeQuotaObservation observation) : ICreditBudget
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CreditBudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var (used, quota, reservePercent, maxPerRun) = await LoadParamsAsync(cancellationToken);
        var reserve = CreditBudgetRules.ComputeReserve(quota, reservePercent);
        return new CreditBudgetSnapshot(
            Used: used,
            Quota: quota,
            Reserve: reserve,
            SpendableScheduled: CreditBudgetRules.ComputeSpendable(quota, used, reservePercent, isManual: false),
            SpendableManual: CreditBudgetRules.ComputeSpendable(quota, used, reservePercent, isManual: true),
            MaxCreditsPerRun: maxPerRun);
    }

    public async Task<bool> TryReserveAsync(
        Guid syncRunId,
        int cost,
        bool isManual,
        CancellationToken cancellationToken = default)
    {
        var run = await syncRunStore.GetAsync(syncRunId, cancellationToken)
            ?? throw new InvalidOperationException($"SyncRun '{syncRunId}' was not found.");

        var (used, quota, reservePercent, maxPerRun) = await LoadParamsAsync(cancellationToken);
        if (!CreditBudgetRules.TryReserve(
                used,
                quota,
                reservePercent,
                maxPerRun,
                run.CreditsUsed,
                cost,
                isManual))
        {
            return false;
        }

        run.CreditsUsed += cost;
        await syncRunStore.UpdateAsync(run, cancellationToken);

        // Optimistic bump until Watchmode headers call ObserveQuotaAsync.
        observation.Observe(used + cost, quota);
        return true;
    }

    public Task ObserveQuotaAsync(int? quotaUsed, int? quotaLimit, CancellationToken cancellationToken = default)
    {
        observation.Observe(quotaUsed, quotaLimit);
        return Task.CompletedTask;
    }

    private async Task<(int Used, int Quota, int ReservePercent, int MaxPerRun)> LoadParamsAsync(
        CancellationToken cancellationToken)
    {
        var usage = await usageReader.GetWatchmodeUsageAsync(cancellationToken);
        var (observedUsed, observedQuota) = observation.Snapshot();
        var used = observedUsed ?? usage.Used;
        var configuredQuota = await GetIntAsync(SettingsKeys.WatchmodeQuota, 2500, cancellationToken);
        var quota = observedQuota ?? usage.QuotaLimit ?? configuredQuota;
        var reservePercent = await GetIntAsync(SettingsKeys.WatchmodeReservePercent, 10, cancellationToken);
        var maxPerRun = await GetIntAsync(SettingsKeys.WatchmodeMaxCreditsPerRun, 80, cancellationToken);
        return (used, quota, reservePercent, maxPerRun);
    }

    private async Task<int> GetIntAsync(string key, int defaultValue, CancellationToken cancellationToken)
    {
        var raw = await settingsStore.GetRawAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return JsonSerializer.Deserialize<int>(raw, JsonOptions);
    }
}
