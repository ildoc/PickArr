using PickArr.Core.Recommendations;

namespace PickArr.Core.Abstractions;

public interface IRecoModelService
{
    Task<RecoModelInfoDto> GetInfoAsync(CancellationToken cancellationToken = default);

    Task DetectAsync(CancellationToken cancellationToken = default);

    Task DownloadAsync(CancellationToken cancellationToken = default);

    /// <summary>Embed texts when model Ready; null if unavailable.</summary>
    float[]? TryEmbed(string text);

    IReadOnlyList<float[]>? TryEmbedBatch(IReadOnlyList<string> texts);

    bool IsReady { get; }
}

public sealed record RecoModelInfoDto(
    string Status,
    string? ModelId,
    long? SizeBytes,
    string? Directory,
    string? LastError);
