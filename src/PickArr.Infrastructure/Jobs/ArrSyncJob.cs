using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;

namespace Pickarr.Infrastructure.Jobs;

public sealed class ArrSyncJob(IArrLibrarySync sync)
{
    public const string SonarrJobId = "arr-sync-sonarr";
    public const string RadarrJobId = "arr-sync-radarr";

    public Task ExecuteSonarrAsync(CancellationToken cancellationToken = default) =>
        sync.SyncAsync(ArrKind.Sonarr, cancellationToken);

    public Task ExecuteRadarrAsync(CancellationToken cancellationToken = default) =>
        sync.SyncAsync(ArrKind.Radarr, cancellationToken);
}
