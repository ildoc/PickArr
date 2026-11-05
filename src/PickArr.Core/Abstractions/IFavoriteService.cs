namespace PickArr.Core.Abstractions;

public interface IFavoriteService
{
    Task AddAsync(int titleId, CancellationToken cancellationToken = default);
    Task RemoveAsync(int titleId, CancellationToken cancellationToken = default);
}
