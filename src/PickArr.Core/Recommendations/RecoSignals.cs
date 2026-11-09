namespace PickArr.Core.Recommendations;

/// <summary>Per-title signal mass s_i and N_eff (BR-REC-02..04, FR-REC-SG-*).</summary>
public static class RecoSignals
{
    public const double DefaultWeightLove = 1.5;
    public const double DefaultWeightLike = 1.0;
    public const double DefaultWeightDislike = -1.0;
    public const double DefaultWeightNotInterested = -0.75;
    public const double DefaultWeightFavorite = 0.5;
    public const double DefaultWeightArrAdd = 0.6;
    public const double DefaultWeightDownloaded = 0.4;
    /// <summary>Hide / already-seen dismiss. Default 0: exclusion only, no taste mass.</summary>
    public const double DefaultWeightWatched = 0;

    public static RecoSignalWeights DefaultWeights { get; } = new(
        Love: DefaultWeightLove,
        Like: DefaultWeightLike,
        Dislike: DefaultWeightDislike,
        NotInterested: DefaultWeightNotInterested,
        Favorite: DefaultWeightFavorite,
        ArrAdd: DefaultWeightArrAdd,
        Downloaded: DefaultWeightDownloaded,
        Watched: DefaultWeightWatched);

    /// <summary>
    /// Builds s_i for every title that has an explicit rating and/or (when enabled) implicit signals.
    /// Explicit rating replaces implicits. Favorites are a temporary “watch later” signal (BR-REC-02).
    /// Watched (Hide) is exclusion from lists; with weight 0 it adds no taste mass. Arr Delete does not
    /// contribute and does not cancel Add. Implicit sum is clamped to [0, +1].
    /// </summary>
    public static RecoSignalResult Compute(
        IReadOnlyList<RecoSignalTitleFacts> titles,
        RecoSignalWeights? weights = null,
        bool useImplicit = true)
    {
        var w = weights ?? DefaultWeights;
        var signals = new Dictionary<int, double>();

        foreach (var title in titles)
        {
            if (title.ExplicitRating is { } rating)
            {
                signals[title.TitleId] = WeightForExplicit(rating, w);
                continue;
            }

            if (!useImplicit)
            {
                continue;
            }

            var implicitSum = 0.0;
            if (title.IsFavorite)
            {
                implicitSum += w.Favorite;
            }

            if (title.HasSuccessfulArrAdd)
            {
                implicitSum += w.ArrAdd;
            }

            if (title.IsDownloaded)
            {
                implicitSum += w.Downloaded;
            }

            if (title.IsWatched)
            {
                implicitSum += w.Watched;
            }

            if (implicitSum <= 0)
            {
                continue;
            }

            signals[title.TitleId] = Math.Clamp(implicitSum, 0, 1);
        }

        var nEff = signals.Values.Sum(Math.Abs);
        return new RecoSignalResult(signals, nEff);
    }

    public static double WeightForExplicit(string rating, RecoSignalWeights weights)
    {
        if (rating.Equals("Dislike", StringComparison.OrdinalIgnoreCase))
        {
            return weights.Dislike;
        }

        if (rating.Equals("NotInterested", StringComparison.OrdinalIgnoreCase)
            || rating.Equals("not_interested", StringComparison.OrdinalIgnoreCase))
        {
            return weights.NotInterested;
        }

        if (rating.Equals("Love", StringComparison.OrdinalIgnoreCase))
        {
            return weights.Love;
        }

        return weights.Like;
    }
}

public sealed record RecoSignalWeights(
    double Love,
    double Like,
    double Dislike,
    double NotInterested,
    double Favorite,
    double ArrAdd,
    double Downloaded,
    double Watched);

/// <param name="ExplicitRating">Normalized Love/Like/Dislike/NotInterested, or null when absent.</param>
/// <param name="HasSuccessfulArrAdd">True when any ArrAuditLog Add+Success exists for the title.</param>
public sealed record RecoSignalTitleFacts(
    int TitleId,
    string? ExplicitRating,
    bool IsFavorite,
    bool HasSuccessfulArrAdd,
    bool IsDownloaded,
    bool IsWatched = false);

public sealed record RecoSignalResult(
    IReadOnlyDictionary<int, double> Signals,
    double EffectiveN);
