namespace Pickarr.Core.Abstractions;

public interface IWatchedService
{
    Task AddAsync(int titleId, CancellationToken cancellationToken = default);
    Task RemoveAsync(int titleId, CancellationToken cancellationToken = default);
}
