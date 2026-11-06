namespace PickArr.Core.Recommendations;

/// <summary>Learning stage and personal weight from effective signal mass (BR-REC-04/05, §12.1).</summary>
public static class RecoLearning
{
    public const string StageCold = "cold";
    public const string StageWarming = "warming";
    public const string StagePersonalized = "personalized";

    public const double DefaultN0 = 30;
    public const double DefaultPriorResidual = 0.1;
    public const double ColdThreshold = 5;

    public static string StageFromEffectiveN(double nEff, double n0 = DefaultN0)
    {
        if (nEff < ColdThreshold)
        {
            return StageCold;
        }

        if (nEff < n0)
        {
            return StageWarming;
        }

        return StagePersonalized;
    }

    /// <summary>
    /// <c>w = min(1, N_eff / N0)</c>; when fully personalized the prior keeps residual ε
    /// so personal weight is at most <c>1 − ε</c>.
    /// </summary>
    public static double PersonalWeight(
        double nEff,
        double n0 = DefaultN0,
        double priorResidual = DefaultPriorResidual)
    {
        if (n0 <= 0)
        {
            return 0;
        }

        var raw = Math.Min(1.0, nEff / n0);
        var maxPersonal = Math.Clamp(1.0 - priorResidual, 0, 1);
        return Math.Min(raw, maxPersonal);
    }

    public static double FuseScore(double prior, double personal, double personalWeight) =>
        (1.0 - personalWeight) * prior + personalWeight * personal;
}
