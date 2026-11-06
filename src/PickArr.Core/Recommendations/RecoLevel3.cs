namespace PickArr.Core.Recommendations;

/// <summary>Feature layout for optional supervised level 3 (§12.6). Training lives in Infrastructure (ML.NET).</summary>
public static class RecoLevel3
{
    public const int DefaultMinRatings = 100;
    public const int HashBuckets = 32;
    public const int ScalarFeatureCount = 6;
    public const int FeatureCount = HashBuckets + ScalarFeatureCount;

    /// <summary>
    /// Features: 32 hashed sparse-term buckets + [Sparse, Knn, Prior, yearNorm, voteNorm, popNorm].
    /// </summary>
    public static float[] BuildFeatures(
        SparseVector? titleVector,
        double sparse,
        double knn,
        double prior,
        int? releaseYear,
        double? voteTmdb,
        double? popularity)
    {
        var features = new float[FeatureCount];
        if (titleVector is not null)
        {
            foreach (var (term, weight) in titleVector.Weights)
            {
                var bucket = (HashTerm(term) & 0x7FFFFFFF) % HashBuckets;
                features[bucket] += (float)weight;
            }
        }

        features[HashBuckets + 0] = (float)sparse;
        features[HashBuckets + 1] = (float)knn;
        features[HashBuckets + 2] = (float)prior;
        features[HashBuckets + 3] = releaseYear is { } y
            ? (float)Math.Clamp((y - 1950) / 80.0, 0, 1)
            : 0.5f;
        features[HashBuckets + 4] = voteTmdb is { } v
            ? (float)Math.Clamp(v / 10.0, 0, 1)
            : 0.5f;
        features[HashBuckets + 5] = popularity is { } p
            ? (float)Math.Clamp(Math.Log10(1 + Math.Max(0, p)) / 3.0, 0, 1)
            : 0.5f;

        return features;
    }

    private static int HashTerm(string term)
    {
        unchecked
        {
            var hash = 2166136261;
            foreach (var ch in term)
            {
                hash ^= ch;
                hash *= 16777619;
            }

            return (int)hash;
        }
    }
}

public sealed record RecoLevel3Example(
    int TitleId,
    bool IsLike,
    float[] Features);
