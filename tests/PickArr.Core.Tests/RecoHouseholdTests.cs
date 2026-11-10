using Pickarr.Core.Domain;
using Pickarr.Core.Recommendations;
using Shouldly;

namespace Pickarr.Core.Tests;

public sealed class RecoHouseholdTests
{
    [Fact]
    public void Without_other_positive_votes_the_score_matches_fuse()
    {
        foreach (var nEff in new[] { 0d, 10d, RecoLearning.DefaultN0, 100d })
        {
            var h = RecoHousehold.SlotWeight(nEff, othersHavePositiveVotes: false);
            h.ShouldBe(0);
            var personalWeight = RecoLearning.PersonalWeight(nEff);
            var priorSlot = RecoHousehold.BlendPrior(prior: 0.4, household: 0.9, h);
            priorSlot.ShouldBe(0.4);
            RecoLearning.FuseScore(priorSlot, personal: 0.8, personalWeight)
                .ShouldBe(RecoLearning.FuseScore(0.4, 0.8, personalWeight));
        }
    }

    [Fact]
    public void At_cold_start_the_prior_keeps_half_of_the_non_personal_slot()
    {
        var h = RecoHousehold.SlotWeight(nEff: 0, othersHavePositiveVotes: true);
        h.ShouldBe(0.5);
        (1 - h).ShouldBe(0.5);

        var priorSlot = RecoHousehold.BlendPrior(prior: 0.4, household: 1, h);
        priorSlot.ShouldBe(0.7);
        priorSlot.ShouldBeGreaterThan(0.4);
    }

    [Fact]
    public void At_or_above_n0_the_household_term_is_zero()
    {
        RecoHousehold.SlotWeight(RecoLearning.DefaultN0, othersHavePositiveVotes: true).ShouldBe(0);
        RecoHousehold.SlotWeight(RecoLearning.DefaultN0 + 10, othersHavePositiveVotes: true).ShouldBe(0);
        RecoHousehold.SlotWeight(nEff: 15, othersHavePositiveVotes: true).ShouldBe(0.25);
    }

    [Fact]
    public void Average_uses_other_love_and_like_only()
    {
        var signals = RecoHousehold.AveragePositive(
        [
            new RecoHouseholdVote(1, 10, RatingValues.Love),
            new RecoHouseholdVote(2, 10, RatingValues.Love),
            new RecoHouseholdVote(3, 10, RatingValues.Like),
            new RecoHouseholdVote(2, 11, RatingValues.Dislike),
            new RecoHouseholdVote(3, 12, RatingValues.NotInterested),
            new RecoHouseholdVote(2, 13, RatingValues.Love),
        ],
            excludeProfileId: 1);

        signals[10].ShouldBe((RecoSignals.DefaultWeightLove + RecoSignals.DefaultWeightLike) / 2);
        signals.ContainsKey(11).ShouldBeFalse();
        signals.ContainsKey(12).ShouldBeFalse();
        signals[13].ShouldBe(RecoSignals.DefaultWeightLove);

        RecoHousehold.AveragePositive(
            [new RecoHouseholdVote(1, 10, RatingValues.Love)],
            excludeProfileId: 1).Count.ShouldBe(0);
    }
}
