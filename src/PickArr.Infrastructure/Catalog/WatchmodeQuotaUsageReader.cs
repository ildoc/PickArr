using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Catalog.Watchmode;
using PickArr.Infrastructure.Persistence;
using System.Text.Json;

namespace PickArr.Infrastructure.Catalog;

public sealed class WatchmodeQuotaUsageReader(
    PickArrDbContext db,
    ISettingsStore settingsStore,
    IClock clock) : IQuotaUsageReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<QuotaUsage> GetWatchmodeUsageAsync(CancellationToken cancellationToken = default)
    {
        var latestWithQuota = await db.ApiUsages.AsNoTracking()
            .Where(x => x.Service == WatchmodeApiUsageHandler.ServiceName && x.QuotaUsed != null)
            .OrderByDescending(x => x.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestWithQuota?.QuotaUsed is { } headerUsed)
        {
            return new QuotaUsage(headerUsed, latestWithQuota.QuotaLimit);
        }

        var billingDay = await GetBillingDayAsync(cancellationToken);
        var periodStart = ComputePeriodStart(clock.UtcNow, billingDay);
        var sum = await db.ApiUsages.AsNoTracking()
            .Where(x => x.Service == WatchmodeApiUsageHandler.ServiceName && x.Timestamp >= periodStart)
            .SumAsync(x => (int?)x.Credits, cancellationToken) ?? 0;

        return new QuotaUsage(sum, QuotaLimit: null);
    }

    private async Task<int> GetBillingDayAsync(CancellationToken cancellationToken)
    {
        var raw = await settingsStore.GetRawAsync(SettingsKeys.WatchmodeBillingDayOfMonth, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 1;
        }

        var day = JsonSerializer.Deserialize<int>(raw, JsonOptions);
        return Math.Clamp(day, 1, 28);
    }

    public static DateTime ComputePeriodStart(DateTime utcNow, int billingDayOfMonth)
    {
        billingDayOfMonth = Math.Clamp(billingDayOfMonth, 1, 28);
        var candidate = new DateTime(utcNow.Year, utcNow.Month, billingDayOfMonth, 0, 0, 0, DateTimeKind.Utc);
        if (utcNow < candidate)
        {
            candidate = candidate.AddMonths(-1);
        }

        return candidate;
    }
}
