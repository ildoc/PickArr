namespace Pickarr.Infrastructure.Metadata.Tmdb;

public sealed class TmdbOptions
{
    public const string SectionName = "Pickarr:Tmdb";

    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3/";
    public double RequestsPerSecond { get; set; } = 20;
    public bool DisableResilience { get; set; }
}
