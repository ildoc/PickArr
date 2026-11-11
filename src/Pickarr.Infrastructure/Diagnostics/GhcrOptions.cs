namespace Pickarr.Infrastructure.Diagnostics;

public sealed class GhcrOptions
{
    public const string SectionName = "Pickarr:Ghcr";

    /// <summary>Registry origin (no trailing slash). Overridable in tests.</summary>
    public string BaseUrl { get; set; } = "https://ghcr.io";

    /// <summary>Lowercase owner/image path used by the OCI tags API.</summary>
    public string Repository { get; set; } = "ildoc/pickarr";

    /// <summary>Human-readable source string returned to the UI.</summary>
    public string DisplaySource { get; set; } = "ghcr.io/ildoc/pickarr";

    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(8);
}
