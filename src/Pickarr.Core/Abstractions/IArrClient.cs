using Pickarr.Core.Arr;
using Pickarr.Core.Domain;

namespace Pickarr.Core.Abstractions;

public interface IArrClient
{
    ArrKind Kind { get; }
    Task<ArrSystemInfo> GetSystemInfoAsync(CancellationToken cancellationToken = default);
    Task<ArrOptions> GetOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls the server with the supplied credentials and returns quality profiles and root folders.
    /// Does not read or write stored settings.
    /// </summary>
    Task<ArrConnectionProbe> ProbeAsync(string baseUrl, string apiKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArrItemSnapshot>> GetLibraryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArrQueueEntry>> GetQueueAsync(CancellationToken cancellationToken = default);
    Task<ArrItemSnapshot?> FindByExternalIdAsync(ArrExternalId id, CancellationToken cancellationToken = default);
    Task<ArrItemSnapshot> AddAsync(ArrAddRequest request, CancellationToken cancellationToken = default);
    Task SearchAsync(int arrId, CancellationToken cancellationToken = default);
    Task DeleteAsync(int arrId, bool deleteFiles, bool addImportExclusion, CancellationToken cancellationToken = default);
}

public interface IArrClientFactory
{
    Task<IArrClient?> TryCreateAsync(ArrKind kind, CancellationToken cancellationToken = default);

    IArrClient Get(ArrKind kind);
}
