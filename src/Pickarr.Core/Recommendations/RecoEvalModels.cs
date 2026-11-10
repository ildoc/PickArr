namespace Pickarr.Core.Recommendations;

public sealed record RecoEvalReportDto(
    DateTime EvaluatedAt,
    int RatingCount,
    int LikeCount,
    int DislikeCount,
    int PoolSize,
    bool LowSampleWarning,
    RecoEvalLevelMetrics? Prior,
    RecoEvalLevelMetrics? Level1,
    RecoEvalLevelMetrics? Level2,
    RecoEvalLevelMetrics? Combined,
    RecoEvalLevelMetrics? Level3,
    double? LikeVsDislikeAucCombined,
    bool Level3Active,
    string? Level3SkipReason,
    IReadOnlyDictionary<string, double>? SuggestedDefaults);

public sealed record RecoEvalScoreBundle(
    double Prior,
    double Level1,
    double Level2,
    double Combined,
    double? Level3);
