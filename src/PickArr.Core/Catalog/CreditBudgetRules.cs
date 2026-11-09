namespace Pickarr.Core.Catalog;

/// <summary>
/// Pure credit-budget rules (spec §6.2 / BR-14).
/// </summary>
public static class CreditBudgetRules
{
    public static int ComputeReserve(int quota, int reservePercent)
    {
        if (quota <= 0 || reservePercent <= 0)
        {
            return 0;
        }

        return (int)Math.Ceiling(quota * (reservePercent / 100d));
    }

    public static int ComputeSpendable(int quota, int used, int reservePercent, bool isManual)
    {
        used = Math.Max(0, used);
        quota = Math.Max(0, quota);
        if (isManual)
        {
            return Math.Max(0, quota - used);
        }

        var reserve = ComputeReserve(quota, reservePercent);
        return Math.Max(0, quota - used - reserve);
    }

    public static bool TryReserve(
        int used,
        int quota,
        int reservePercent,
        int maxCreditsPerRun,
        int runCreditsUsed,
        int cost,
        bool isManual)
    {
        if (cost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost));
        }

        if (cost == 0)
        {
            return true;
        }

        if (runCreditsUsed + cost > maxCreditsPerRun)
        {
            return false;
        }

        var spendable = ComputeSpendable(quota, used, reservePercent, isManual);
        return cost <= spendable;
    }
}
