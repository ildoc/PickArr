using Microsoft.ML;
using Microsoft.ML.Data;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Recommendations;

namespace Pickarr.Infrastructure.Metadata.Reco;

/// <summary>Regularized logistic regression via ML.NET (§12.6).</summary>
public sealed class RecoLevel3Model : IRecoLevel3Model
{
    public IRecoLevel3Scorer? TryTrain(
        IReadOnlyList<RecoLevel3Example> examples,
        int minRatings,
        int minPerClass)
    {
        if (examples.Count < minRatings)
        {
            return null;
        }

        var likes = examples.Count(e => e.IsLike);
        var dislikes = examples.Count - likes;
        if (likes < minPerClass || dislikes < minPerClass)
        {
            return null;
        }

        var ml = new MLContext(seed: 42);
        var rows = examples.Select(e => new TrainRow
        {
            Features = e.Features,
            Label = e.IsLike,
        }).ToList();

        var data = ml.Data.LoadFromEnumerable(rows);
        var pipeline = ml.BinaryClassification.Trainers.LbfgsLogisticRegression(
            labelColumnName: nameof(TrainRow.Label),
            featureColumnName: nameof(TrainRow.Features),
            l2Regularization: 0.1f);

        var model = pipeline.Fit(data);
        var engine = ml.Model.CreatePredictionEngine<TrainRow, ScoreRow>(model);
        return new Scorer(engine);
    }

    private sealed class Scorer(PredictionEngine<TrainRow, ScoreRow> engine) : IRecoLevel3Scorer
    {
        public double Score(float[] features)
        {
            var pred = engine.Predict(new TrainRow { Features = features, Label = false });
            return Math.Clamp(pred.Probability, 0, 1);
        }
    }

    private sealed class TrainRow
    {
        [VectorType(RecoLevel3.FeatureCount)]
        public float[] Features { get; set; } = [];

        public bool Label { get; set; }
    }

    private sealed class ScoreRow
    {
        [ColumnName("Probability")]
        public float Probability { get; set; }
    }
}
