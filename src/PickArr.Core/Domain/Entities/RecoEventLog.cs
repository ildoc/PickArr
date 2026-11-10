namespace Pickarr.Core.Domain.Entities;

/// <summary>Local recommendation interaction log (FR-REC-AD-05). Written only when events are enabled.</summary>
public sealed class RecoEventLog
{
    public long Id { get; set; }
    public int ProfileId { get; set; }
    public DateTime At { get; set; }
    public int TitleId { get; set; }
    public string Event { get; set; } = string.Empty;
    public int? Version { get; set; }
    public int? Rank { get; set; }
    public string? Scope { get; set; }

    public Title? Title { get; set; }
}
