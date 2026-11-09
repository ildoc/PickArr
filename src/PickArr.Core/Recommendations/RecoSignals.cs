namespace PickArr.Core.Recommendations;

/// <summary>Per-title signal mass s_i and N_eff (BR-REC-02..04, FR-REC-SG-*).</summary>
public static class RecoSignals
{
    public const double DefaultWeightLike = 1.0;
    public const double DefaultWeightDislike = -1.0;
    public const double DefaultWeightFavorite = 0.5;
    public const double DefaultWeightArrAdd = 0.6;
    public const double DefaultWeightDownloaded = 0.4;

    public static RecoSignalWeights DefaultWeights { get; } = new(
        Like: DefaultWeightLike,
        Dislike: DefaultWeightDislike,
        Favorite: DefaultWeightFavorite,
        ArrAdd: DefaultWeightArrAdd,
        Downloaded: DefaultWeightDownloaded);

    /// <summary>
    /// Builds s_i for every title that has an explicit rating and/or (when enabled) implicit signals.
    /// Explicit rating replaces implicits. Favorites are a temporary “watch later” signal (BR-REC-02).
    /// Arr Delete does not contribute and does not cancel Add. Implicit sum is clamped to [0, +1].
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
                signals[title.TitleId] = rating.Equals("Dislike", StringComparison.OrdinalIgnoreCase)
                    ? w.Dislike
                    : w.Like;
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

            if (implicitSum <= 0)
            {
                continue;
            }

            signals[title.TitleId] = Math.Clamp(implicitSum, 0, 1);
        }

        var nEff = signals.Values.Sum(Math.Abs);
        return new RecoSignalResult(signals, nEff);
    }
}

public sealed record RecoSignalWeights(
    double Like,
    double Dislike,
    double Favorite,
    double ArrAdd,
    double Downloaded);

/// <param name="ExplicitRating">Normalized Like/Dislike, or null when absent.</param>
/// <param name="HasSuccessfulArrAdd">True when any ArrAuditLog Add+Success exists for the title.</param>
public sealed record RecoSignalTitleFacts(
    int TitleId,
    string? ExplicitRating,
    bool IsFavorite,
    bool HasSuccessfulArrAdd,
    bool IsDownloaded);

public sealed record RecoSignalResult(
    IReadOnlyDictionary<int, double> Signals,
    double EffectiveN);
