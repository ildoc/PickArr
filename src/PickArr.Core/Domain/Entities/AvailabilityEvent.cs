namespace PickArr.Core.Domain.Entities;

public sealed class AvailabilityEvent
{
    public int Id { get; set; }
    public int AvailabilityId { get; set; }
    public required string Type { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid? SyncRunId { get; set; }
    public string? Details { get; set; }

    public Availability? Availability { get; set; }
}
