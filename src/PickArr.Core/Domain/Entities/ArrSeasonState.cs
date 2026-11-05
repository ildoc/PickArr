namespace PickArr.Core.Domain.Entities;

public sealed class ArrSeasonState
{
    public int ArrLibraryItemId { get; set; }
    public int SeasonNumber { get; set; }
    public bool Monitored { get; set; }
    public int EpisodeFileCount { get; set; }
    public int EpisodeCount { get; set; }
    public int TotalEpisodeCount { get; set; }
    public long SizeOnDisk { get; set; }
    public required string State { get; set; }
    public double? QueueProgress { get; set; }

    public ArrLibraryItem? ArrLibraryItem { get; set; }
}
