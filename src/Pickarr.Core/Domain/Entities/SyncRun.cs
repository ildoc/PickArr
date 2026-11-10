namespace Pickarr.Core.Domain.Entities;

public sealed class SyncRun
{
    public Guid Id { get; set; }
    public required string Kind { get; set; }
    public int? CatalogSourceId { get; set; }
    public required string TriggeredBy { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public required string Status { get; set; }
    public int PagesFetched { get; set; }
    public int CreditsUsed { get; set; }
    public int TitlesAdded { get; set; }
    public int TitlesUpdated { get; set; }
    public int TitlesRemoved { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CheckpointJson { get; set; }

    public CatalogSource? CatalogSource { get; set; }
}
