namespace Pickarr.Core.Abstractions;

public interface IWatchedService
{
    Task AddAsync(int profileId, int titleId, CancellationToken cancellationToken = default);
    Task RemoveAsync(int profileId, int titleId, CancellationToken cancellationToken = default);
}
