namespace Pickarr.Core.Recommendations;

/// <summary>
/// Amplifies local positive signals along cached TMDB recommendation edges that already
/// intersect the catalog pool. Decays with <c>N_eff</c> so it does not compete with a
/// mature sparse/kNN personal score.
/// </summary>
public static class RecoTmdbBoost
{
    /// <summary>Max share of the personal slot given to the TMDB graph at cold start.</summary>
    public const double DefaultMaxShare = 0.25;

    public const int MaxRank = 20;

    /// <summary>Similar-titles blend: additive boost for a direct TMDB edge.</summary>
    public const double DefaultSimilarEdgeBoost = 0.15;

    public static double AmplifierWeight(
        double nEff,
        double n0 = RecoLearning.DefaultN0,
        double maxShare = DefaultMaxShare)
    {
        if (maxShare <= 0 || n0 <= 0)
        {
            return 0;
        }

        var raw = Math.Min(1.0, Math.Max(0, nEff) / n0);
        return maxShare * (1.0 - raw);
    }

    public static double BlendPersonal(double basePersonal, double tmdbNorm, double ampWeight)
    {
        if (ampWeight <= 0)
        {
            return basePersonal;
        }

        if (ampWeight >= 1)
        {
            return tmdbNorm;
        }

        return (1.0 - ampWeight) * basePersonal + ampWeight * tmdbNorm;
    }

    /// <summary>Linear decay over the first <see cref="MaxRank"/> TMDB ranks (1 = strongest).</summary>
    public static double RankWeight(int rank)
    {
        if (rank < 1)
        {
            return 0;
        }

        var capped = Math.Min(rank, MaxRank + 1);
        return Math.Max(0, (MaxRank + 1 - capped) / (double)MaxRank);
    }

    /// <summary>
    /// Propagate positive local signals one hop along TMDB edges. Negative-signal sources
    /// do not contribute. Related titles with a negative local signal are skipped so
    /// dislike is not overridden by the global TMDB graph.
    /// </summary>
    public static IReadOnlyDictionary<int, double> ScoreAll(
        IReadOnlyDictionary<int, double> signals,
        IReadOnlyList<RecoTmdbEdge> edges,
        IReadOnlySet<int>? poolTitleIds = null)
    {
        var scores = new Dictionary<int, double>();
        if (signals.Count == 0 || edges.Count == 0)
        {
            return scores;
        }

        foreach (var edge in edges)
        {
            if (poolTitleIds is not null && !poolTitleIds.Contains(edge.RelatedTitleId))
            {
                continue;
            }

            if (edge.SourceTitleId == edge.RelatedTitleId)
            {
                continue;
            }

            if (!signals.TryGetValue(edge.SourceTitleId, out var sourceSignal) || sourceSignal <= 0)
            {
                continue;
            }

            if (signals.TryGetValue(edge.RelatedTitleId, out var relatedSignal) && relatedSignal < 0)
            {
                continue;
            }

            var contrib = sourceSignal * RankWeight(edge.Rank);
            if (contrib <= 0)
            {
                continue;
            }

            scores[edge.RelatedTitleId] = scores.GetValueOrDefault(edge.RelatedTitleId) + contrib;
        }

        return scores;
    }

    /// <summary>
    /// Strongest positive-signal source that points at <paramref name="titleId"/> via a TMDB edge.
    /// Used for <c>similarTo</c> reasons.
    /// </summary>
    public static (int SourceTitleId, double Contribution)? TopContributor(
        int titleId,
        IReadOnlyDictionary<int, double> signals,
        IReadOnlyList<RecoTmdbEdge> edges)
    {
        var bestSource = 0;
        var best = 0.0;
        foreach (var edge in edges)
        {
            if (edge.RelatedTitleId != titleId || edge.SourceTitleId == titleId)
            {
                continue;
            }

            if (!signals.TryGetValue(edge.SourceTitleId, out var sourceSignal) || sourceSignal <= 0)
            {
                continue;
            }

            var contrib = sourceSignal * RankWeight(edge.Rank);
            if (contrib > best)
            {
                best = contrib;
                bestSource = edge.SourceTitleId;
            }
        }

        return bestSource > 0 ? (bestSource, best) : null;
    }

    /// <summary>
    /// Merge local similarity with direct TMDB recommendation edges from the query title.
    /// </summary>
    public static IReadOnlyList<(int TitleId, double Similarity)> MergeSimilar(
        IReadOnlyList<(int TitleId, double Similarity)> local,
        IReadOnlyList<(int TitleId, int Rank)> outgoingEdges,
        int limit,
        IReadOnlySet<int> exclude,
        double edgeBoost = DefaultSimilarEdgeBoost)
    {
        var scores = new Dictionary<int, double>();
        foreach (var (titleId, similarity) in local)
        {
            if (exclude.Contains(titleId))
            {
                continue;
            }

            scores[titleId] = similarity;
        }

        if (edgeBoost > 0)
        {
            foreach (var (titleId, rank) in outgoingEdges)
            {
                if (exclude.Contains(titleId))
                {
                    continue;
                }

                var boost = edgeBoost * RankWeight(rank);
                if (boost <= 0)
                {
                    continue;
                }

                scores[titleId] = scores.GetValueOrDefault(titleId) + boost;
            }
        }

        return scores
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Take(Math.Max(0, limit))
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }
}

public readonly record struct RecoTmdbEdge(int SourceTitleId, int RelatedTitleId, int Rank);
