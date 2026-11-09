namespace Pickarr.Core.Abstractions;

public sealed record QuotaUsage(int Used, int? QuotaLimit);

public interface IQuotaUsageReader
{
    Task<QuotaUsage> GetWatchmodeUsageAsync(CancellationToken cancellationToken = default);
}
