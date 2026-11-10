namespace Pickarr.Core.Recommendations;

/// <summary>
/// Cold-start household prior (D-006). Level-1 only: the caller scores the averaged
/// Love/Like vector with the sparse profile and does not run kNN on it.
/// </summary>
public static class RecoHousehold
{
    /// <summary>Cap on the non-personal slot given to other profiles. The rest stays the TMDB prior.</summary>
    public const double MaxShare = 0.5;

    public static double SlotWeight(
        double nEff,
        bool othersHavePositiveVotes,
        double n0 = RecoLearning.DefaultN0)
    {
        if (!othersHavePositiveVotes || n0 <= 0)
        {
            return 0;
        }

        var raw = Math.Min(1.0, Math.Max(0, nEff) / n0);
        return MaxShare * (1.0 - raw);
    }

    /// <summary><c>(1 - h) * prior + h * household</c>. At <c>h = 0</c> this is the prior.</summary>
    public static double BlendPrior(double prior, double household, double slotWeight) =>
        (1.0 - slotWeight) * prior + slotWeight * household;

    /// <summary>
    /// Mean Love/Like weight per title. The profile being built is excluded.
    /// A profile that did not vote on the title is not in the mean.
    /// Dislike and NotInterested do not enter.
    /// </summary>
    public static IReadOnlyDictionary<int, double> AveragePositive(
        IEnumerable<RecoHouseholdVote> votes,
        int excludeProfileId,
        RecoSignalWeights? weights = null)
    {
        var w = weights ?? RecoSignals.DefaultWeights;
        var sums = new Dictionary<int, (double Sum, int Count)>();
        foreach (var vote in votes)
        {
            if (vote.ProfileId == excludeProfileId || !IsLoveOrLike(vote.Rating))
            {
                continue;
            }

            var weight = RecoSignals.WeightForExplicit(vote.Rating, w);
            if (weight <= 0)
            {
                continue;
            }

            var current = sums.GetValueOrDefault(vote.TitleId);
            sums[vote.TitleId] = (current.Sum + weight, current.Count + 1);
        }

        return sums.ToDictionary(kv => kv.Key, kv => kv.Value.Sum / kv.Value.Count);
    }

    private static bool IsLoveOrLike(string rating) =>
        rating.Equals("Love", StringComparison.OrdinalIgnoreCase)
        || rating.Equals("Like", StringComparison.OrdinalIgnoreCase);
}

public readonly record struct RecoHouseholdVote(int ProfileId, int TitleId, string Rating);
