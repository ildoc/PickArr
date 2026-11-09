namespace Pickarr.Core.Domain.Entities;

public sealed class TitleLocalization
{
    public int TitleId { get; set; }
    public required string Language { get; set; }
    public required string Name { get; set; }
    public required string NameNormalized { get; set; }
    public string? Overview { get; set; }
    public string? Tagline { get; set; }
    public DateTime FetchedAt { get; set; }

    public Title? Title { get; set; }
}
