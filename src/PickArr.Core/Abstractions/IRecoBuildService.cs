namespace Pickarr.Core.Abstractions;

public interface IRecoBuildService
{
    /// <summary>Rebuild every profile. Returns the last published version.</summary>
    Task<int> BuildAsync(CancellationToken cancellationToken = default);

    /// <summary>Rebuild one profile. Returns its new version.</summary>
    Task<int> BuildAsync(int profileId, CancellationToken cancellationToken = default);
}
