using Microsoft.Extensions.Options;

namespace PickArr.Infrastructure.Metadata.Tmdb;

public sealed class TmdbRateLimitHandler(IOptions<TmdbOptions> options, TmdbRateGate rateGate) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var rps = options.Value.RequestsPerSecond;
        var minInterval = rps <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(1d / rps);
        return rateGate.SendAsync(
            ct => base.SendAsync(request, ct),
            minInterval,
            cancellationToken);
    }
}
