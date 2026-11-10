namespace Pickarr.Core.Domain.Entities;

/// <summary>One row per profile. Model fields are read and written only on the admin row.</summary>
public sealed class RecoState
{
    public const int SingletonId = 1;

    public int Id { get; set; }
    public int ProfileId { get; set; }
    public int CurrentVersion { get; set; }
    public DateTime? BuiltAt { get; set; }
    public int RatingCount { get; set; }
    public double EffectiveN { get; set; }
    public string Stage { get; set; } = "cold";
    public string ModelStatus { get; set; } = "Absent";
    public string? ModelId { get; set; }
    public double EmbeddingCoverage { get; set; }
    public string? ProfileJson { get; set; }
    public string? LastError { get; set; }
    public string? LastEvalJson { get; set; }
    public DateTime? LastEvalAt { get; set; }
    public bool Level3Active { get; set; }
}
