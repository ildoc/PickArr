namespace PickArr.Core.Recommendations;

/// <summary>Level-0 Bayesian + popularity prior (§12.2).</summary>
public static class RecoPrior
{
    public const double VoteDamping = 300;
    public const double BayesWeight = 0.7;
    public const double PopWeight = 0.3;

    public static IReadOnlyList<RecoPriorScore> Compute(IReadOnlyList<RecoTitleStats> pool)
    {
        if (pool.Count == 0)
        {
            return [];
        }

        var votes = pool.Where(t => t.VoteTmdb is not null).Select(t => t.VoteTmdb!.Value).ToList();
        var c = votes.Count == 0 ? 0.0 : votes.Average();

        var bayesRaw = new double[pool.Count];
        var popRaw = new double[pool.Count];
        var hasPopularity = new bool[pool.Count];

        for (var i = 0; i < pool.Count; i++)
        {
            var t = pool[i];
            if (t.VoteTmdb is null)
            {
                bayesRaw[i] = c;
            }
            else
            {
                var v = Math.Max(0, t.VoteTmdbCount ?? 0);
                var r = t.VoteTmdb.Value;
                var m = VoteDamping;
                bayesRaw[i] = (v / (v + m)) * r + (m / (v + m)) * c;
            }

            if (t.Popularity is null)
            {
                popRaw[i] = 0;
                hasPopularity[i] = false;
            }
            else
            {
                popRaw[i] = Math.Log(1 + Math.Max(0, t.Popularity.Value));
                hasPopularity[i] = true;
            }
        }

        var bayesNorm = MinMaxNormalize(bayesRaw);
        var popNormAll = MinMaxNormalize(popRaw);
        var results = new List<RecoPriorScore>(pool.Count);

        for (var i = 0; i < pool.Count; i++)
        {
            // Missing popularity → pop component forced to 0 (doc §12.2).
            var pop = hasPopularity[i] ? popNormAll[i] : 0.0;
            var prior = BayesWeight * bayesNorm[i] + PopWeight * pop;
            results.Add(new RecoPriorScore(
                pool[i].TitleId,
                Prior: prior,
                Bayes: bayesRaw[i],
                BayesNorm: bayesNorm[i],
                Pop: pop));
        }

        return results
            .OrderByDescending(r => r.Prior)
            .ThenBy(r => r.TitleId)
            .ToList();
    }

    internal static double[] MinMaxNormalize(IReadOnlyList<double> values)
    {
        var result = new double[values.Count];
        if (values.Count == 0)
        {
            return result;
        }

        var min = values[0];
        var max = values[0];
        for (var i = 1; i < values.Count; i++)
        {
            if (values[i] < min)
            {
                min = values[i];
            }

            if (values[i] > max)
            {
                max = values[i];
            }
        }

        if (Math.Abs(max - min) < double.Epsilon)
        {
            for (var i = 0; i < values.Count; i++)
            {
                result[i] = 0.5;
            }

            return result;
        }

        var span = max - min;
        for (var i = 0; i < values.Count; i++)
        {
            result[i] = (values[i] - min) / span;
        }

        return result;
    }
}

public sealed record RecoTitleStats(
    int TitleId,
    double? VoteTmdb,
    int? VoteTmdbCount,
    double? Popularity);

public sealed record RecoPriorScore(
    int TitleId,
    double Prior,
    double Bayes,
    double BayesNorm,
    double Pop);
