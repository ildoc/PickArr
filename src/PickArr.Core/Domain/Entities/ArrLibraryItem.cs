namespace PickArr.Core.Domain.Entities;

public sealed class ArrLibraryItem
{
    public int Id { get; set; }
    public required string Kind { get; set; }
    public int ArrId { get; set; }
    public int? TmdbId { get; set; }
    public int? TvdbId { get; set; }
    public string? ImdbId { get; set; }
    public int? TitleId { get; set; }
    public required string ArrTitle { get; set; }
    public int? Year { get; set; }
    public bool Monitored { get; set; }
    public required string State { get; set; }
    public double? QueueProgress { get; set; }
    public long SizeOnDisk { get; set; }
    public string? Path { get; set; }
    public int? EpisodeFileCount { get; set; }
    public int? EpisodeCount { get; set; }
    public int? TotalEpisodeCount { get; set; }
    public bool? HasFile { get; set; }
    public bool? IsAvailable { get; set; }
    public DateTime SyncedAt { get; set; }

    public Title? Title { get; set; }
    public ICollection<ArrSeasonState> Seasons { get; set; } = new List<ArrSeasonState>();
}
