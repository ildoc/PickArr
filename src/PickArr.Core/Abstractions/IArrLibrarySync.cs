using Pickarr.Core.Arr;
using Pickarr.Core.Domain;

namespace Pickarr.Core.Abstractions;

public interface IArrLibrarySync
{
    Task SyncAsync(ArrKind kind, CancellationToken cancellationToken = default);
    Task UpsertItemAsync(ArrKind kind, ArrItemSnapshot item, CancellationToken cancellationToken = default);
    Task RemoveItemAsync(ArrKind kind, int arrId, CancellationToken cancellationToken = default);
}
