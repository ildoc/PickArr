namespace PickArr.Infrastructure.Metadata.Tmdb;

/// <summary>
/// Process-wide TMDB request spacing so Enrichment, MetadataRefresh, and RecoFeature
/// share one throttle even when Hangfire runs them on overlapping workers.
/// </summary>
public sealed class TmdbRateGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _nextAllowedUtc = DateTime.MinValue;

    public async Task<HttpResponseMessage> SendAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> send,
        TimeSpan minInterval,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            if (_nextAllowedUtc > now)
            {
                await Task.Delay(_nextAllowedUtc - now, cancellationToken);
            }

            var response = await send(cancellationToken);
            _nextAllowedUtc = DateTime.UtcNow + minInterval;
            return response;
        }
        finally
        {
            _gate.Release();
        }
    }
}
