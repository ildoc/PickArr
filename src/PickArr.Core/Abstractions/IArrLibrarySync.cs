using PickArr.Core.Arr;
using PickArr.Core.Domain;

namespace PickArr.Core.Abstractions;

public interface IArrLibrarySync
{
    Task SyncAsync(ArrKind kind, CancellationToken cancellationToken = default);
    Task UpsertItemAsync(ArrKind kind, ArrItemSnapshot item, CancellationToken cancellationToken = default);
    Task RemoveItemAsync(ArrKind kind, int arrId, CancellationToken cancellationToken = default);
}
