using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Admin;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Catalog;
using PickArr.Infrastructure.Catalog.Watchmode;
using PickArr.Infrastructure.Persistence;

namespace PickArr.Infrastructure.Admin;

public sealed class AdminQuotaService(
    ICreditBudget creditBudget,
    ISettingsStore settings,
    PickArrDbContext db,
    IClock clock) : IAdminQuotaService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AdminQuotaDto> GetWatchmodeQuotaAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await creditBudget.GetSnapshotAsync(cancellationToken);
        var billingDay = await GetBillingDayAsync(cancellationToken);
        var periodStart = WatchmodeQuotaUsageReader.ComputePeriodStart(clock.UtcNow, billingDay);
        var periodEnd = periodStart.AddMonths(1);
        var projected = QuotaProjection.ProjectMonthEnd(snapshot.Used, periodStart, clock.UtcNow, periodEnd);

        var lastUpdated = await db.ApiUsages.AsNoTracking()
            .Where(x => x.Service == WatchmodeApiUsageHandler.ServiceName)
            .OrderByDescending(x => x.Timestamp)
            .Select(x => (DateTime?)x.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        return new AdminQuotaDto(
            Service: "watchmode",
            Quota: snapshot.Quota,
            Used: snapshot.Used,
            Reserve: snapshot.Reserve,
            Spendable: snapshot.SpendableScheduled,
            ProjectedMonthEnd: projected,
            LastUpdatedAt: lastUpdated,
            PeriodStart: periodStart,
            PeriodEnd: periodEnd);
    }

    private async Task<int> GetBillingDayAsync(CancellationToken cancellationToken)
    {
        var raw = await settings.GetRawAsync(SettingsKeys.WatchmodeBillingDayOfMonth, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 1;
        }

        try
        {
            return Math.Clamp(JsonSerializer.Deserialize<int>(raw, JsonOptions), 1, 28);
        }
        catch (JsonException)
        {
            return 1;
        }
    }
}
