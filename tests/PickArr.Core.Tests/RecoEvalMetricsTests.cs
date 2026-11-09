using Pickarr.Core.Recommendations;
using Shouldly;

namespace Pickarr.Core.Tests;

public sealed class RecoEvalMetricsTests
{
    [Fact]
    public void PairwiseAuc_perfect_ranking_is_one()
    {
        RecoEvalMetrics.PairwiseAuc(0.9, [0.1, 0.2, 0.3]).ShouldBe(1.0);
    }

    [Fact]
    public void PairwiseAuc_ties_count_half()
    {
        RecoEvalMetrics.PairwiseAuc(0.5, [0.5, 0.5]).ShouldBe(0.5);
    }

    [Fact]
    public void HitAt_true_when_few_better()
    {
        RecoEvalMetrics.HitAt(0.8, [0.9, 0.1, 0.2], k: 2).ShouldBeTrue();
        RecoEvalMetrics.HitAt(0.8, [0.95, 0.9, 0.85], k: 2).ShouldBeFalse();
    }

    [Fact]
    public void ShouldActivateLevel3_requires_margin_and_counts()
    {
        var l2 = new RecoEvalLevelMetrics(0.70, 0.4, 20);
        var l3 = new RecoEvalLevelMetrics(0.72, 0.5, 20);

        RecoEvalMetrics.ShouldActivateLevel3(l2, l3, likeCount: 50, dislikeCount: 50, minRatings: 100)
            .ShouldBeTrue();

        RecoEvalMetrics.ShouldActivateLevel3(l2, l3, likeCount: 10, dislikeCount: 10, minRatings: 100)
            .ShouldBeFalse();

        RecoEvalMetrics.ShouldActivateLevel3(l2, new RecoEvalLevelMetrics(0.705, 0.4, 20), 50, 50, 100)
            .ShouldBeFalse();
    }

    [Fact]
    public void Level3_features_have_fixed_dimension()
    {
        var features = RecoLevel3.BuildFeatures(
            SparseVector.Empty,
            sparse: 0.4,
            knn: 0.6,
            prior: 0.5,
            releaseYear: 2010,
            voteTmdb: 7.5,
            popularity: 20);
        features.Length.ShouldBe(RecoLevel3.FeatureCount);
    }
}
