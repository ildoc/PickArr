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

        var fetch = await GetTagsAsync(opts, cancellationToken);
        var newer = fetch.Succeeded
            ? ImageVersionComparer.FindNewerTags(currentVersion, fetch.Tags)
            : [];

        return new ImageUpdateDto(
            CurrentVersion: currentVersion,
            Channel: channel,
            NewerTags: newer,
            UpdateAvailable: newer.Count > 0,
            CheckSucceeded: fetch.Succeeded,
            CheckedAt: checkedAt,
            Source: opts.DisplaySource);
    }

    private async Task<ImageTagListResult> GetTagsAsync(GhcrOptions opts, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out ImageTagListResult? cached) && cached is { Succeeded: true })
        {
            return cached;
        }

        try
        {
            var fresh = await tagSource.ListTagsAsync(cancellationToken);
            if (fresh.Succeeded)
            {
                cache.Set(CacheKey, fresh, opts.CacheDuration);
            }
            else
            {
                logger.LogWarning("GHCR image tag check failed; not caching the miss");
            }

            return fresh;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to list GHCR image tags");
            return ImageTagListResult.Failed();
        }
    }
}
