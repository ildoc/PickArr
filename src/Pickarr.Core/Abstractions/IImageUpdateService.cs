using Pickarr.Core.Diagnostics;

namespace Pickarr.Core.Abstractions;

public interface IImageUpdateService
{
    Task<ImageUpdateDto> CheckAsync(string currentVersion, CancellationToken cancellationToken = default);
}

public interface IImageTagSource
{
    Task<IReadOnlyList<string>> ListTagsAsync(CancellationToken cancellationToken = default);
}
