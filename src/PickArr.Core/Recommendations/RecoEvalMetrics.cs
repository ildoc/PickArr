namespace PickArr.Core.Recommendations;

/// <summary>Offline leave-one-out metrics (§17.3).</summary>
public static class RecoEvalMetrics
{
    public const int LowSampleWarningThreshold = 30;
    public const int DefaultNegativesPerFold = 40;
    public const int HitAtK = 10;
    public const int MaxFolds = 50;
    public const double Level3AucMargin = 0.01;
    public const int Level3MinPerClass = 20;

    /// <summary>
    /// Pairwise AUC for one positive against negatives: fraction of negatives scored strictly lower.
    /// Ties count as 0.5.
    /// </summary>
    public static double PairwiseAuc(double positiveScore, IReadOnlyList<double> negativeScores)
    {
        if (negativeScores.Count == 0)
        {
            return 0.5;
        }

        var wins = 0.0;
        foreach (var neg in negativeScores)
        {
            if (positiveScore > neg)
            {
                wins += 1.0;
            }
            else if (Math.Abs(positiveScore - neg) < 1e-12)
            {
                wins += 0.5;
            }
        }

        return wins / negativeScores.Count;
    }

    /// <summary>True when the positive ranks within the top <paramref name="k"/> of {positive + negatives}.</summary>
    public static bool HitAt(double positiveScore, IReadOnlyList<double> negativeScores, int k = HitAtK)
    {
        if (k <= 0)
        {
            return false;
        }

        // Treat as hit when fewer than k others score strictly higher (ties keep the positive in top-k).
        var strictlyBetter = negativeScores.Count(n => n > positiveScore);
        return strictlyBetter < k;
    }

    public static RecoEvalLevelMetrics Aggregate(IReadOnlyList<(double Auc, bool Hit)> folds)
    {
        if (folds.Count == 0)
        {
            return new RecoEvalLevelMetrics(0, 0, 0);
        }

        var auc = folds.Average(f => f.Auc);
        var hit = folds.Count(f => f.Hit) / (double)folds.Count;
        return new RecoEvalLevelMetrics(auc, hit, folds.Count);
    }

    /// <summary>AUC treating Like as positive and Dislike as negative on a scored list.</summary>
    public static double LikeVsDislikeAuc(
        IReadOnlyList<(bool IsLike, double Score)> labelled)
    {
        var likes = labelled.Where(x => x.IsLike).Select(x => x.Score).ToList();
        var dislikes = labelled.Where(x => !x.IsLike).Select(x => x.Score).ToList();
        if (likes.Count == 0 || dislikes.Count == 0)
        {
            return 0.5;
        }

        var sum = 0.0;
        foreach (var like in likes)
        {
            sum += PairwiseAuc(like, dislikes);
        }

        return sum / likes.Count;
    }

    public static bool ShouldActivateLevel3(
        RecoEvalLevelMetrics? level2,
        RecoEvalLevelMetrics? level3,
        int likeCount,
        int dislikeCount,
        int minRatings,
        double margin = Level3AucMargin,
        int minPerClass = Level3MinPerClass)
    {
        if (level2 is null || level3 is null)
        {
            return false;
        }

        if (likeCount + dislikeCount < minRatings || likeCount < minPerClass || dislikeCount < minPerClass)
        {
            return false;
        }

        if (level3.Folds < Math.Min(10, MaxFolds / 2))
        {
            return false;
        }

        return level3.Auc >= level2.Auc + margin;
    }
}

public sealed record RecoEvalLevelMetrics(double Auc, double HitAt10, int Folds);
