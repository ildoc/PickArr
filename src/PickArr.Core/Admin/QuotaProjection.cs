namespace Pickarr.Core.Admin;

public static class QuotaProjection
{
    /// <summary>
    /// Linear projection of usage to period end: used / elapsedDays * totalDays (FR-ADM-04).
    /// </summary>
    public static int ProjectMonthEnd(int used, DateTime periodStart, DateTime utcNow, DateTime periodEnd)
    {
        used = Math.Max(0, used);
        var elapsed = Math.Max(1.0, (utcNow - periodStart).TotalDays);
        var total = Math.Max(elapsed, (periodEnd - periodStart).TotalDays);
        return (int)Math.Round(used / elapsed * total, MidpointRounding.AwayFromZero);
    }
}
