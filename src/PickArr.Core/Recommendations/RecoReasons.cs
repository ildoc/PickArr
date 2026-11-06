using System.Text.Json;
using System.Text.Json.Serialization;

namespace PickArr.Core.Recommendations;

public static class RecoReasons
{
    public const string Popular = "popular";
    public const string SimilarTo = "similarTo";
    public const string Terms = "terms";
    public const string Explore = "explore";

    public const double DefaultSimilarToMinSimilarity = 0.15;
    public const int MaxReasons = 3;
    public const int MaxSimilarTo = 2;
    public const int MaxTerms = 3;

    /// <summary>L0 cold-start reason payload (keys only; UI builds copy).</summary>
    public const string PopularOnlyJson = "[{\"kind\":\"popular\"}]";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Serialize(IReadOnlyList<RecoReasonDto> reasons) =>
        JsonSerializer.Serialize(reasons, JsonOptions);

    /// <summary>
    /// Up to 3 reasons: similarTo (positive-signal neighbours), terms (profile overlap), popular
    /// when prior dominates personal contribution (§12.7).
    /// </summary>
    /// <summary>
    /// Precompute positive-signal title vectors once per rebuild (NFR-REC-02).
    /// </summary>
    public static IReadOnlyDictionary<int, SparseVector> BuildPositiveCandidates(
        IReadOnlyDictionary<int, double> signals,
        IReadOnlyDictionary<int, SparseVector> titleVectors)
    {
        var positive = new Dictionary<int, SparseVector>();
        foreach (var (id, weight) in signals)
        {
            if (weight > 0 && titleVectors.TryGetValue(id, out var vector))
            {
                positive[id] = vector;
            }
        }

        return positive;
    }

    public static IReadOnlyList<RecoReasonDto> Build(
        int titleId,
        double priorNorm,
        double personalNorm,
        double personalWeight,
        SparseVector? profile,
        SparseVector? titleVector,
        IReadOnlyDictionary<int, SparseVector> titleVectors,
        IReadOnlyDictionary<int, double> signals,
        IReadOnlyDictionary<int, string> titleNames,
        Func<string, string?>? resolveTermLabel = null,
        double similarMin = DefaultSimilarToMinSimilarity,
        IReadOnlyDictionary<int, SparseVector>? positiveCandidates = null)
    {
        var reasons = new List<RecoReasonDto>(MaxReasons);

        if (profile is not null && !profile.IsEmpty && titleVectors.Count > 0)
        {
            var candidates = positiveCandidates
                ?? BuildPositiveCandidates(signals, titleVectors);

            if (candidates.Count > 0 && titleVectors.TryGetValue(titleId, out var self))
            {
                var similar = RecoSparse.TopSimilar(
                    self,
                    candidates,
                    MaxSimilarTo + 1,
                    similarMin);
                var similarAdded = 0;
                foreach (var (neighborId, sim) in similar)
                {
                    if (neighborId == titleId)
                    {
                        continue;
                    }

                    if (reasons.Count >= MaxReasons || similarAdded >= MaxSimilarTo)
                    {
                        break;
                    }

                    titleNames.TryGetValue(neighborId, out var name);
                    reasons.Add(new RecoReasonDto(
                        SimilarTo,
                        TitleId: neighborId,
                        Name: name,
                        Similarity: Math.Round(sim, 4)));
                    similarAdded++;
                }
            }

            if (reasons.Count < MaxReasons
                && titleVector is not null
                && !titleVector.IsEmpty)
            {
                var termKeys = RecoSparse.TopMatchingTerms(profile, titleVector, MaxTerms);
                var labels = termKeys
                    .Select(key => resolveTermLabel?.Invoke(key) ?? key)
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .Take(MaxTerms)
                    .ToList();
                if (labels.Count > 0)
                {
                    reasons.Add(new RecoReasonDto(Terms, Terms: labels));
                }
            }
        }

        // Popular when the prior still carries most of the fused score.
        var priorContribution = (1.0 - personalWeight) * priorNorm;
        var personalContribution = personalWeight * personalNorm;
        if (reasons.Count < MaxReasons
            && (personalWeight < 0.05 || priorContribution >= personalContribution))
        {
            reasons.Add(new RecoReasonDto(Popular));
        }

        if (reasons.Count == 0)
        {
            reasons.Add(new RecoReasonDto(Popular));
        }

        return reasons;
    }
}

/// <summary>Min-max helpers shared by prior / sparse / fusion (§12.1).</summary>
public static class RecoNormalize
{
    public static double[] MinMax(IReadOnlyList<double> values) => RecoPrior.MinMaxNormalize(values);

    /// <summary>Maps cosine-like scores (often in [-1,1]) onto [0,1] via pool min-max.</summary>
    public static IReadOnlyDictionary<int, double> MinMaxByTitle(
        IReadOnlyDictionary<int, double> scores)
    {
        if (scores.Count == 0)
        {
            return scores;
        }

        var ids = scores.Keys.ToList();
        var raw = ids.Select(id => scores[id]).ToArray();
        var norm = MinMax(raw);
        var result = new Dictionary<int, double>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            result[ids[i]] = norm[i];
        }

        return result;
    }
}
