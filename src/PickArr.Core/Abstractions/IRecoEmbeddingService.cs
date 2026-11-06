namespace PickArr.Core.Abstractions;

public interface IRecoEmbeddingService
{
    /// <summary>Embed missing/stale titles in the enabled pool. Returns processed count.</summary>
    Task<int> EmbedPendingAsync(bool rebuildAll = false, CancellationToken cancellationToken = default);
}
