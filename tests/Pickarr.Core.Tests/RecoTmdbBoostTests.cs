using Pickarr.Core.Recommendations;
using Shouldly;

namespace Pickarr.Core.Tests;

public sealed class RecoTmdbBoostTests
{
    [Fact]
    public void Amplifier_weight_decays_to_zero_at_n0()
    {
        RecoTmdbBoost.AmplifierWeight(nEff: 0).ShouldBe(RecoTmdbBoost.DefaultMaxShare);
        RecoTmdbBoost.AmplifierWeight(RecoLearning.DefaultN0).ShouldBe(0);
        RecoTmdbBoost.AmplifierWeight(nEff: 15).ShouldBe(RecoTmdbBoost.DefaultMaxShare * 0.5);
    }

    [Fact]
    public void ScoreAll_propagates_only_positive_sources_along_edges()
    {
        var signals = new Dictionary<int, double>
        {
            [1] = 1.0,
            [2] = -1.0,
            [9] = -0.5,
        };
        var edges = new List<RecoTmdbEdge>
        {
            new(1, 10, Rank: 1),
            new(1, 11, Rank: 5),
            new(2, 12, Rank: 1),
            new(1, 9, Rank: 1), // disliked target skipped
        };

        var scores = RecoTmdbBoost.ScoreAll(signals, edges);

        scores.Keys.OrderBy(id => id).ShouldBe([10, 11]);
        scores[10].ShouldBeGreaterThan(scores[11]);
        scores.ContainsKey(12).ShouldBeFalse();
        scores.ContainsKey(9).ShouldBeFalse();
    }

    [Fact]
    public void BlendPersonal_only_moves_when_amp_weight_positive()
    {
        RecoTmdbBoost.BlendPersonal(0.2, 1.0, ampWeight: 0).ShouldBe(0.2);
        RecoTmdbBoost.BlendPersonal(0.2, 1.0, ampWeight: 0.25).ShouldBe(0.2 * 0.75 + 1.0 * 0.25);
    }

    [Fact]
    public void MergeSimilar_boosts_direct_tmdb_edges()
    {
        var local = new List<(int TitleId, double Similarity)>
        {
            (10, 0.5),
            (11, 0.4),
        };
        var edges = new List<(int TitleId, int Rank)>
        {
            (10, 1),
            (12, 1),
        };

        var merged = RecoTmdbBoost.MergeSimilar(local, edges, limit: 3, exclude: new HashSet<int> { 99 });

        merged[0].TitleId.ShouldBe(10);
        merged[0].Similarity.ShouldBeGreaterThan(0.5);
        merged.Select(m => m.TitleId).ShouldContain(12);
    }

    [Fact]
    public void TopContributor_picks_strongest_positive_source()
    {
        var signals = new Dictionary<int, double> { [1] = 1.0, [2] = 0.5 };
        var edges = new List<RecoTmdbEdge>
        {
            new(1, 10, Rank: 10), // 1.0 * 0.55 = 0.55
            new(2, 10, Rank: 1),  // 0.5 * 1.0 = 0.50
        };

        var top = RecoTmdbBoost.TopContributor(10, signals, edges);
        top.ShouldNotBeNull();
        top!.Value.SourceTitleId.ShouldBe(1);
    }
}
