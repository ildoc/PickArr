namespace Pickarr.Core.Domain.Entities;

public sealed class Title
{
    public int Id { get; set; }
    public int? WatchmodeId { get; set; }
    public int? TmdbId { get; set; }
    public string? TmdbType { get; set; }
    public string? ImdbId { get; set; }
    public int? TvdbId { get; set; }
    public required string MediaType { get; set; }
    public required string Category { get; set; }
    public string? OriginalTitle { get; set; }
    public string? OriginalTitleNormalized { get; set; }
    public int? ReleaseYear { get; set; }
    public int? RuntimeMinutes { get; set; }
    public int? SeasonCount { get; set; }
    public int? EpisodeCount { get; set; }
    public double? VoteTmdb { get; set; }
    public int? VoteTmdbCount { get; set; }
    public double? Popularity { get; set; }
    public string? OriginalLanguage { get; set; }
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
    public string? CastJson { get; set; }
    public string? SeasonsJson { get; set; }
    /// <summary>JSON array of TMDB origin_country codes (optional reco feature).</summary>
    public string? OriginCountries { get; set; }
    public required string MetadataStatus { get; set; }
    public DateTime? MetadataFetchedAt { get; set; }
    public int MetadataAttempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Availability> Availabilities { get; set; } = new List<Availability>();
    public ICollection<TitleLocalization> Localizations { get; set; } = new List<TitleLocalization>();
    public ICollection<TitleGenre> TitleGenres { get; set; } = new List<TitleGenre>();
    public ICollection<TitleKeyword> TitleKeywords { get; set; } = new List<TitleKeyword>();
    public ICollection<TitleCredit> TitleCredits { get; set; } = new List<TitleCredit>();
    public Favorite? Favorite { get; set; }
    public Watched? Watched { get; set; }
    public Rating? Rating { get; set; }
}
