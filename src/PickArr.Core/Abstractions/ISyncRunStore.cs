using PickArr.Core.Domain.Entities;

namespace PickArr.Core.Abstractions;

public interface ISyncRunStore
{
    Task<SyncRun?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SyncRun> AddAsync(SyncRun run, CancellationToken cancellationToken = default);
    Task UpdateAsync(SyncRun run, CancellationToken cancellationToken = default);

    Task<SyncRun> StartAsync(
        string kind,
        int? catalogSourceId,
        string triggeredBy,
        string? checkpointJson = null,
        CancellationToken cancellationToken = default);

    Task PauseAsync(Guid id, string checkpointJson, CancellationToken cancellationToken = default);
    Task SucceedAsync(Guid id, CancellationToken cancellationToken = default);
    Task FailAsync(Guid id, string errorMessage, CancellationToken cancellationToken = default);
    Task MarkNeedsReviewAsync(Guid id, string? errorMessage = null, CancellationToken cancellationToken = default);
}
