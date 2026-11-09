namespace Pickarr.Core.Domain.Entities;

public sealed class Recommendation
{
    public int Version { get; set; }
    public int TitleId { get; set; }
    public double Score { get; set; }
    public double? Personal { get; set; }
    public double Prior { get; set; }
    public int Rank { get; set; }
    public string ReasonsJson { get; set; } = "[]";

    public Title? Title { get; set; }
}
