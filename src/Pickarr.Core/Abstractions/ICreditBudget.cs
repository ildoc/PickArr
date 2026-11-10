namespace Pickarr.Core.Abstractions;

public sealed record CreditBudgetSnapshot(
    int Used,
    int Quota,
    int Reserve,
    int SpendableScheduled,
    int SpendableManual,
    int MaxCreditsPerRun);

public interface ICreditBudget
{
    Task<CreditBudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reserves <paramref name="cost"/> credits against monthly budget and the run's max-per-run cap.
    /// On success increments <c>SyncRun.CreditsUsed</c>. Does not call the remote API.
    /// </summary>
    Task<bool> TryReserveAsync(
        Guid syncRunId,
        int cost,
        bool isManual,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates known quota headers after a Watchmode response (source of truth for <c>used</c>).
    /// </summary>
    Task ObserveQuotaAsync(int? quotaUsed, int? quotaLimit, CancellationToken cancellationToken = default);
}
