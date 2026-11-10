namespace Pickarr.Core.Abstractions;

/// <summary>Debounced enqueue of RecoBuildJob after rating changes (BR-REC-12).</summary>
public interface IRecoRebuildScheduler
{
    /// <summary>Replace that profile's pending delayed rebuild with one scheduled after the debounce window.</summary>
    void ScheduleDebouncedRebuild(int profileId);
}
