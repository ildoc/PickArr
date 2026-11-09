using PickArr.Core.Recommendations;
using Shouldly;

namespace PickArr.Core.Tests;

public sealed class RecoSignalsTests
{
    [Fact]
    public void Explicit_like_overrides_implicits_and_sets_n_eff()
    {
        var result = RecoSignals.Compute(
        [
            new RecoSignalTitleFacts(1, "Like", IsFavorite: true, HasSuccessfulArrAdd: true, IsDownloaded: true),
            new RecoSignalTitleFacts(2, null, IsFavorite: true, HasSuccessfulArrAdd: false, IsDownloaded: false),
        ]);

        result.Signals[1].ShouldBe(1.0);
        result.Signals[2].ShouldBe(0.5);
        result.EffectiveN.ShouldBe(1.5);
    }

    [Fact]
    public void Explicit_love_uses_higher_weight_than_like()
    {
        var result = RecoSignals.Compute(
        [
            new RecoSignalTitleFacts(1, "Love", IsFavorite: false, HasSuccessfulArrAdd: false, IsDownloaded: false),
            new RecoSignalTitleFacts(2, "Like", IsFavorite: false, HasSuccessfulArrAdd: false, IsDownloaded: false),
        ]);

        result.Signals[1].ShouldBe(1.5);
        result.Signals[2].ShouldBe(1.0);
        result.EffectiveN.ShouldBe(2.5);
    }

    [Fact]
    public void Explicit_dislike_ignores_implicits()
    {
        var result = RecoSignals.Compute(
        [
            new RecoSignalTitleFacts(1, "Dislike", IsFavorite: true, HasSuccessfulArrAdd: true, IsDownloaded: true),
        ]);

        result.Signals[1].ShouldBe(-1.0);
        result.EffectiveN.ShouldBe(1.0);
    }

    [Fact]
    public void Implicit_sum_is_clamped_to_one()
    {
        var result = RecoSignals.Compute(
        [
            new RecoSignalTitleFacts(1, null, IsFavorite: true, HasSuccessfulArrAdd: true, IsDownloaded: true),
        ]);

        // 0.5 + 0.6 + 0.4 = 1.5 → clamp [0,1]
        result.Signals[1].ShouldBe(1.0);
    }

    [Fact]
    public void UseImplicit_false_skips_favorites_and_arr()
    {
        var result = RecoSignals.Compute(
        [
            new RecoSignalTitleFacts(1, null, IsFavorite: true, HasSuccessfulArrAdd: true, IsDownloaded: true),
            new RecoSignalTitleFacts(2, "Like", IsFavorite: false, HasSuccessfulArrAdd: false, IsDownloaded: false),
        ],
            useImplicit: false);

        result.Signals.ContainsKey(1).ShouldBeFalse();
        result.Signals[2].ShouldBe(1.0);
        result.EffectiveN.ShouldBe(1.0);
    }

    [Fact]
    public void Arr_add_without_delete_negation_still_counts()
    {
        // Delete is not modelled as a negative fact — HasSuccessfulArrAdd stays true.
        var result = RecoSignals.Compute(
        [
            new RecoSignalTitleFacts(1, null, IsFavorite: false, HasSuccessfulArrAdd: true, IsDownloaded: false),
        ]);

        result.Signals[1].ShouldBe(0.6);
    }

    [Fact]
    public void Watched_alone_contributes_default_weight()
    {
        var result = RecoSignals.Compute(
        [
            new RecoSignalTitleFacts(1, null, IsFavorite: false, HasSuccessfulArrAdd: false, IsDownloaded: false, IsWatched: true),
        ]);

        result.Signals[1].ShouldBe(0.35);
        result.EffectiveN.ShouldBe(0.35);
    }

    [Fact]
    public void Explicit_rating_overrides_watched()
    {
        var result = RecoSignals.Compute(
        [
            new RecoSignalTitleFacts(1, "Like", IsFavorite: false, HasSuccessfulArrAdd: false, IsDownloaded: false, IsWatched: true),
        ]);

        result.Signals[1].ShouldBe(1.0);
    }

    [Fact]
    public void UseImplicit_false_skips_watched()
    {
        var result = RecoSignals.Compute(
        [
            new RecoSignalTitleFacts(1, null, IsFavorite: false, HasSuccessfulArrAdd: false, IsDownloaded: false, IsWatched: true),
        ],
            useImplicit: false);

        result.Signals.ContainsKey(1).ShouldBeFalse();
    }
}
