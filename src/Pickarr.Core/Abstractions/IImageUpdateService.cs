using Pickarr.Core.Diagnostics;

namespace Pickarr.Core.Abstractions;

public interface IImageUpdateService
{
    Task<ImageUpdateDto> CheckAsync(string currentVersion, CancellationToken cancellationToken = default);
}

public interface IImageTagSource
{
    Task<ImageTagListResult> ListTagsAsync(CancellationToken cancellationToken = default);
}

public sealed record ImageTagListResult(bool Succeeded, IReadOnlyList<string> Tags)
{
    public static ImageTagListResult Ok(IReadOnlyList<string> tags) => new(true, tags);

    public static ImageTagListResult Failed() => new(false, []);
}
