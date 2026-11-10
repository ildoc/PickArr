using Pickarr.Core.Recommendations;
using Shouldly;

namespace Pickarr.Core.Tests;

public sealed class RecoMmrExploreTests
{
    [Fact]
    public void Mmr_prefers_diverse_second_pick()
    {
        var ranked = new List<(int TitleId, double Score)>
        {
            (1, 1.0),
            (2, 0.99), // similar to 1
            (3, 0.80), // different
        };

        double Sim(int a, int b) =>
            (a, b) switch
            {
                (1, 2) or (2, 1) => 0.95,
                (1, 3) or (3, 1) => 0.1,
                (2, 3) or (3, 2) => 0.1,
                _ => 0,
            };

        var order = RecoMmr.RerankIds(ranked, Sim, lambda: 0.7);
        order[0].ShouldBe(1);
        order[1].ShouldBe(3); // diversity beats near-duplicate 2
    }

    [Fact]
    public void Explore_inject_is_deterministic_for_version()
    {
        var ranked = Enumerable.Range(1, 20).ToList();
        var explorers = Enumerable.Range(100, 10).ToList();
        var a = RecoExplore.Inject(ranked, explorers, rate: 0.2, versionSeed: 42);
        var b = RecoExplore.Inject(ranked, explorers, rate: 0.2, versionSeed: 42);
        a.ShouldBe(b);
        a.ShouldContain(id => id >= 100);

        // rate too small for one item → unchanged
        RecoExplore.Inject([1], [100], rate: 0.1, versionSeed: 1).ShouldBe([1]);
    }

    [Fact]
    public void Onboarding_covers_genres_greedily()
    {
        var pool = new List<RecoOnboardingCandidate>
        {
            new(1, 100, [10]),
            new(2, 90, [10]),
            new(3, 80, [20]),
            new(4, 70, [30]),
        };
        var picked = RecoExplore.PickOnboarding(pool, 3);
        picked.Count.ShouldBe(3);
        picked.ShouldContain(1);
        picked.ShouldContain(3);
        picked.ShouldContain(4);
    }

    [Fact]
    public void Onboarding_refresh_picks_next_cover_after_excluding_shown()
    {
        var pool = new List<RecoOnboardingCandidate>
        {
            new(1, 100, [10]),
            new(2, 90, [10]),
            new(3, 80, [20]),
            new(4, 70, [30]),
            new(5, 60, [40]),
        };
        var first = RecoExplore.PickOnboarding(pool, 3);
        var remaining = pool.Where(c => !first.Contains(c.TitleId)).ToList();
        var second = RecoExplore.PickOnboarding(remaining, 3);

        second.Count.ShouldBe(2);
        second.ShouldNotContain(id => first.Contains(id));
        second.ShouldContain(2);
        second.ShouldContain(5);
    }

    [Fact]
    public void Embedding_text_hash_is_stable()
    {
        var text = RecoEmbeddingText.Build(
            "Dark",
            2017,
            ["Crime", "Mystery"],
            ["time travel", "cave"],
            "What if?",
            "A family disappears.");
        var hash = RecoEmbeddingText.Sha256Hex(text);
        hash.Length.ShouldBe(64);
        RecoEmbeddingText.Sha256Hex(text).ShouldBe(hash);
    }
}
