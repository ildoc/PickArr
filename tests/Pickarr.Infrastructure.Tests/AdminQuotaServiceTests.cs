using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Admin;
using Pickarr.Infrastructure.Catalog.Watchmode;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class AdminQuotaServiceTests
{
    [Fact]
    public async Task GetWatchmodeQuotaAsync_maps_snapshot_period_and_projection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickarrDbContext>().UseSqlite(connection).Options;
        await using var db = new PickarrDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var sampledAt = new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);
        db.ApiUsages.Add(new ApiUsage
        {
            Id = Guid.NewGuid(),
            Service = WatchmodeApiUsageHandler.ServiceName,
            Endpoint = "/list-titles",
            Timestamp = sampledAt,
            Credits = 1,
            StatusCode = 200,
        });
        await db.SaveChangesAsync();

        var budget = Substitute.For<ICreditBudget>();
        budget.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new CreditBudgetSnapshot(
                Used: 412,
                Quota: 2500,
                Reserve: 250,
                SpendableScheduled: 1838,
                SpendableManual: 2000,
                MaxCreditsPerRun: 80));

        var settings = Substitute.For<ISettingsStore>();
        settings.GetRawAsync(SettingsKeys.WatchmodeBillingDayOfMonth, Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(1));

        var now = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        var sut = new AdminQuotaService(budget, settings, db, new FixedClock(now));

        var dto = await sut.GetWatchmodeQuotaAsync();

        dto.Service.ShouldBe("watchmode");
        dto.Quota.ShouldBe(2500);
        dto.Used.ShouldBe(412);
        dto.Reserve.ShouldBe(250);
        dto.Spendable.ShouldBe(1838);
        dto.LastUpdatedAt.ShouldBe(sampledAt);
        dto.PeriodStart.ShouldBe(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        dto.PeriodEnd.ShouldBe(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc));
        dto.ProjectedMonthEnd.ShouldNotBeNull();
        dto.ProjectedMonthEnd!.Value.ShouldBeGreaterThanOrEqualTo(412);
    }

    [Fact]
    public async Task GetWatchmodeQuotaAsync_defaults_billing_day_when_setting_missing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickarrDbContext>().UseSqlite(connection).Options;
        await using var db = new PickarrDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var budget = Substitute.For<ICreditBudget>();
        budget.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new CreditBudgetSnapshot(0, 2500, 250, 2250, 2250, 80));
        var settings = Substitute.For<ISettingsStore>();
        settings.GetRawAsync(SettingsKeys.WatchmodeBillingDayOfMonth, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var now = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var sut = new AdminQuotaService(budget, settings, db, new FixedClock(now));

        var dto = await sut.GetWatchmodeQuotaAsync();

        dto.PeriodStart.Day.ShouldBe(1);
        dto.LastUpdatedAt.ShouldBeNull();
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
