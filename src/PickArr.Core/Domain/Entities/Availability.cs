namespace PickArr.Core.Domain.Entities;

public sealed class Availability
{
    public int Id { get; set; }
    public int TitleId { get; set; }
    public int CatalogSourceId { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? AddedAt { get; set; }
    public bool IsBaseline { get; set; }
    public int MissedFullScans { get; set; }
    public DateTime? RemovedAt { get; set; }
    public DateOnly? LeavingDate { get; set; }

    public Title? Title { get; set; }
    public CatalogSource? CatalogSource { get; set; }
    public ICollection<AvailabilityEvent> Events { get; set; } = new List<AvailabilityEvent>();
}
