namespace PickArr.Core.Domain.Entities;

public sealed class ArrAuditLog
{
    public long Id { get; set; }
    public DateTime At { get; set; }
    public required string Action { get; set; }
    public required string Kind { get; set; }
    public int? ArrId { get; set; }
    public int? TitleId { get; set; }
    public required string Options { get; set; }
    public required string Outcome { get; set; }
    public string? Error { get; set; }
}
