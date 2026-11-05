namespace PickArr.Core.Domain.Entities;

public sealed class Provider
{
    public int Id { get; set; }
    public required string Key { get; set; }
    public required string DisplayName { get; set; }
    public required string ProviderType { get; set; }
    public string? ExternalId { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<CatalogSource> CatalogSources { get; set; } = new List<CatalogSource>();
}
