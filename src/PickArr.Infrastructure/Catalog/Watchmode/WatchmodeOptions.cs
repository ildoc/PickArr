namespace Pickarr.Infrastructure.Catalog.Watchmode;

public sealed class WatchmodeOptions
{
    public const string SectionName = "Pickarr:Watchmode";

    public string BaseUrl { get; set; } = "https://api.watchmode.com/v1/";

    /// <summary>Outbound rate limit (requests per second). Spec default: 1.</summary>
    public double RequestsPerSecond { get; set; } = 1;

    public int MaxPageSize { get; set; } = 250;

    /// <summary>When true, skips Microsoft.Extensions.Http.Resilience (useful in WireMock tests).</summary>
    public bool DisableResilience { get; set; }
}
