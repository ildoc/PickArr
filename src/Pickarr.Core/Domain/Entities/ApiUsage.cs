namespace Pickarr.Core.Domain.Entities;

public sealed class ApiUsage
{
    public Guid Id { get; set; }
    public required string Service { get; set; }
    public required string Endpoint { get; set; }
    public DateTime Timestamp { get; set; }
    public int Credits { get; set; }
    public int? QuotaUsed { get; set; }
    public int? QuotaLimit { get; set; }
    public int StatusCode { get; set; }
    public Guid? SyncRunId { get; set; }
}
