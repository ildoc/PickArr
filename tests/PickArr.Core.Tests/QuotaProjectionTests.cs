using PickArr.Core.Admin;
using Shouldly;

namespace PickArr.Core.Tests;

public sealed class QuotaProjectionTests
{
    [Fact]
    public void ProjectMonthEnd_scales_linearly()
    {
        var start = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc); // 10 days elapsed
        var end = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc); // 30 days

        // 100 used in 10 days → 300 projected over 30 days
        QuotaProjection.ProjectMonthEnd(100, start, now, end).ShouldBe(300);
    }

    [Fact]
    public void ProjectMonthEnd_uses_at_least_one_elapsed_day()
    {
        var start = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        var now = start.AddHours(1);
        var end = start.AddMonths(1);

        QuotaProjection.ProjectMonthEnd(10, start, now, end).ShouldBeGreaterThan(10);
    }
}
