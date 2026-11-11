using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Diagnostics;

namespace Pickarr.Infrastructure.Diagnostics;

public sealed class ImageUpdateService(
    IImageTagSource tagSource,
    IMemoryCache cache,
    IOptions<GhcrOptions> options,
    IClock clock,
    ILogger<ImageUpdateService> logger) : IImageUpdateService
{
    private const string CacheKey = "pickarr:ghcr-image-tags";

    public async Task<ImageUpdateDto> CheckAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var checkedAt = new DateTimeOffset(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc));
        var channel = ImageVersionComparer.ResolveChannel(currentVersion);

        IReadOnlyList<string> tags;
        try
        {
            tags = await cache.GetOrCreateAsync(
                CacheKey,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = opts.CacheDuration;
                    return await tagSource.ListTagsAsync(cancellationToken);
                }) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to list GHCR image tags");
            tags = [];
        }

        var newer = ImageVersionComparer.FindNewerTags(currentVersion, tags);
        return new ImageUpdateDto(
            CurrentVersion: currentVersion,
            Channel: channel,
            NewerTags: newer,
            UpdateAvailable: newer.Count > 0,
            CheckedAt: checkedAt,
            Source: opts.DisplaySource);
    }
}
