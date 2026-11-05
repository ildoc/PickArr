namespace PickArr.Core.Domain.Entities;

public sealed class CatalogSource
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public required string CountryCode { get; set; }
    public required string MetadataLanguage { get; set; }
    public bool IsEnabled { get; set; }
    public string IncrementalCron { get; set; } = "0 4 * * *";
    public string FullSyncCron { get; set; } = "0 3 * * 0";
    public int IncrementalPages { get; set; } = 3;
    public DateTime? BaselineCompletedAt { get; set; }
    public DateTime? LastIncrementalAt { get; set; }
    public DateTime? LastFullAt { get; set; }
    public int? LastFullTotalPages { get; set; }

    public Provider? Provider { get; set; }
}
