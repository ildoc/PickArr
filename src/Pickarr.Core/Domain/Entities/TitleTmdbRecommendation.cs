namespace Pickarr.Core.Domain.Entities;

/// <summary>
/// In-catalog edge from TMDB <c>/recommendations</c>: only related titles that already exist locally.
/// </summary>
public sealed class TitleTmdbRecommendation
{
    public int SourceTitleId { get; set; }
    public int RelatedTitleId { get; set; }

    /// <summary>1-based rank on the TMDB recommendations page.</summary>
    public int Rank { get; set; }

    public Title? SourceTitle { get; set; }
    public Title? RelatedTitle { get; set; }
}
