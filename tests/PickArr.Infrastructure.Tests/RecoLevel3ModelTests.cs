using Pickarr.Core.Recommendations;
using Pickarr.Infrastructure.Metadata.Reco;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class RecoLevel3ModelTests
{
    [Fact]
    public void TryTrain_returns_null_under_min_ratings()
    {
        var model = new RecoLevel3Model();
        var examples = Enumerable.Range(0, 40)
            .Select(i => new RecoLevel3Example(
                i,
                i % 2 == 0,
                RecoLevel3.BuildFeatures(null, 0.4, 0.5, 0.5, 2000 + i, 6, 10)))
            .ToList();

        model.TryTrain(examples, minRatings: 100, minPerClass: 20).ShouldBeNull();
    }

    [Fact]
    public void TryTrain_scores_when_thresholds_met()
    {
        var model = new RecoLevel3Model();
        var examples = new List<RecoLevel3Example>();
        for (var i = 0; i < 60; i++)
        {
            // Separable: likes have high sparse, dislikes low.
            examples.Add(new RecoLevel3Example(
                i,
                IsLike: true,
                RecoLevel3.BuildFeatures(null, sparse: 0.9, knn: 0.8, prior: 0.6, 2015, 8, 30)));
            examples.Add(new RecoLevel3Example(
                1000 + i,
                IsLike: false,
                RecoLevel3.BuildFeatures(null, sparse: 0.1, knn: 0.2, prior: 0.4, 1980, 4, 2)));
        }

        var scorer = model.TryTrain(examples, minRatings: 100, minPerClass: 20);
        scorer.ShouldNotBeNull();

        var likeScore = scorer!.Score(RecoLevel3.BuildFeatures(null, 0.9, 0.8, 0.6, 2015, 8, 30));
        var dislikeScore = scorer.Score(RecoLevel3.BuildFeatures(null, 0.1, 0.2, 0.4, 1980, 4, 2));
        likeScore.ShouldBeGreaterThan(dislikeScore);
    }
}
