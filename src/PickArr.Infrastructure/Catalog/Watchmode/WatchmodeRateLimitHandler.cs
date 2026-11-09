using Microsoft.Extensions.Options;

namespace Pickarr.Infrastructure.Catalog.Watchmode;

/// <summary>
/// Simple fixed-interval limiter (default 1 req/s) for Watchmode outbound calls.
/// </summary>
public sealed class WatchmodeRateLimitHandler(IOptions<WatchmodeOptions> options) : DelegatingHandler
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _nextAllowedUtc = DateTime.MinValue;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var rps = options.Value.RequestsPerSecond;
        var minInterval = rps <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(1d / rps);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            if (_nextAllowedUtc > now)
            {
                await Task.Delay(_nextAllowedUtc - now, cancellationToken);
            }

            var response = await base.SendAsync(request, cancellationToken);
            _nextAllowedUtc = DateTime.UtcNow + minInterval;
            return response;
        }
        finally
        {
            _gate.Release();
        }
    }
}
