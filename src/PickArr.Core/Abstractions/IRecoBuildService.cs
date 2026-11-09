namespace Pickarr.Core.Abstractions;

public interface IRecoBuildService
{
    /// <summary>Rebuild recommendations (L0 prior + L1 sparse when signals exist). Returns the new version.</summary>
    Task<int> BuildAsync(CancellationToken cancellationToken = default);
}
