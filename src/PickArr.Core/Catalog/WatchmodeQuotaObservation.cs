namespace Pickarr.Core.Catalog;

/// <summary>
/// Process-wide last Watchmode quota headers. Scoped <see cref="CreditBudgetService"/>
/// instances share this so ObserveQuota survives Hangfire/HTTP scopes.
/// </summary>
public sealed class WatchmodeQuotaObservation
{
    private readonly object _gate = new();
    private int? _used;
    private int? _quota;

    public void Observe(int? quotaUsed, int? quotaLimit)
    {
        lock (_gate)
        {
            if (quotaUsed is { } used)
            {
                _used = used;
            }

            if (quotaLimit is { } limit)
            {
                _quota = limit;
            }
        }
    }

    public (int? Used, int? Quota) Snapshot()
    {
        lock (_gate)
        {
            return (_used, _quota);
        }
    }
}
