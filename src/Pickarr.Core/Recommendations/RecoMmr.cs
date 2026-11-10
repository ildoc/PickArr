namespace Pickarr.Core.Recommendations;

/// <summary>Maximal Marginal Relevance re-ranking in read path (§12.8).</summary>
public static class RecoMmr
{
    public const double DefaultLambda = 0.7;

    /// <summary>
    /// First-N after filters for MMR (§12.8 suggests 200; 120 keeps NFR-REC-01 at 5k).
    /// </summary>
    public const int DefaultCandidateCap = 120;

    /// <summary>
    /// Greedy MMR over the first <paramref name="candidateCap"/> scored items.
    /// <paramref name="similarity"/> returns sim in [0,1] (or cosine) between two title ids.
    /// When lambda ≤ 0, returns the original order unchanged.
    /// </summary>
    public static IReadOnlyList<T> Rerank<T>(
        IReadOnlyList<T> ranked,
        Func<T, int> titleId,
        Func<int, int, double> similarity,
        double lambda = DefaultLambda,
        int candidateCap = DefaultCandidateCap)
    {
        if (ranked.Count <= 1 || lambda <= 0)
        {
            return ranked;
        }

        var pool = ranked.Take(Math.Max(candidateCap, 1)).ToList();
        var rest = ranked.Skip(pool.Count).ToList();
        var selected = new List<T>(pool.Count);
        var remaining = pool.ToList();

        while (remaining.Count > 0)
        {
            T? best = default;
            var bestScore = double.NegativeInfinity;
            var bestIndex = -1;

            for (var i = 0; i < remaining.Count; i++)
            {
                var item = remaining[i];
                var id = titleId(item);
                // Original list position encodes score order; use inverse rank as Score proxy
                // when T does not carry score — callers should pass score via similarity side channel.
                // Here we expect ranked already sorted by Score desc; use position weight.
                var relevance = 1.0 - (double)ranked.ToList().FindIndex(x => titleId(x) == id)
                    / Math.Max(1, ranked.Count);

                var maxSim = 0.0;
                foreach (var picked in selected)
                {
                    maxSim = Math.Max(maxSim, similarity(id, titleId(picked)));
                }

                var mmr = lambda * relevance - (1.0 - lambda) * maxSim;
                if (mmr > bestScore)
                {
                    bestScore = mmr;
                    best = item;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || best is null)
            {
                break;
            }

            selected.Add(best);
            remaining.RemoveAt(bestIndex);
        }

        if (rest.Count == 0)
        {
            return selected;
        }

        return selected.Concat(rest).ToList();
    }

    /// <summary>MMR using explicit scores (preferred).</summary>
    public static IReadOnlyList<int> RerankIds(
        IReadOnlyList<(int TitleId, double Score)> ranked,
        Func<int, int, double> similarity,
        double lambda = DefaultLambda,
        int candidateCap = DefaultCandidateCap)
    {
        if (ranked.Count <= 1 || lambda <= 0)
        {
            return ranked.Select(r => r.TitleId).ToList();
        }

        var scoreById = ranked.ToDictionary(r => r.TitleId, r => r.Score);
        var pool = ranked.Take(Math.Max(candidateCap, 1)).Select(r => r.TitleId).ToList();
        var rest = ranked.Skip(pool.Count).Select(r => r.TitleId).ToList();
        var selected = new List<int>(pool.Count);
        var remaining = pool.ToList();

        while (remaining.Count > 0)
        {
            var bestId = remaining[0];
            var bestScore = double.NegativeInfinity;

            foreach (var id in remaining)
            {
                var maxSim = 0.0;
                foreach (var picked in selected)
                {
                    maxSim = Math.Max(maxSim, similarity(id, picked));
                }

                var mmr = lambda * scoreById[id] - (1.0 - lambda) * maxSim;
                if (mmr > bestScore)
                {
                    bestScore = mmr;
                    bestId = id;
                }
            }

            selected.Add(bestId);
            remaining.Remove(bestId);
        }

        selected.AddRange(rest);
        return selected;
    }
}
