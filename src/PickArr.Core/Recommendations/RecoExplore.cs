namespace PickArr.Core.Recommendations;

/// <summary>Exploration slot injection (§12.8–12.9).</summary>
public static class RecoExplore
{
    public const double DefaultRate = 0.10;

    /// <summary>
    /// Injects explore titles into deterministic slots. Seed from recommendation version.
    /// When rate ≤ 0, returns the list unchanged. Explore candidates: mid-high score, genres
    /// never seen in positive profile terms (caller supplies the candidate set).
    /// </summary>
    public static IReadOnlyList<int> Inject(
        IReadOnlyList<int> rankedIds,
        IReadOnlyList<int> exploreCandidateIds,
        double rate = DefaultRate,
        int versionSeed = 0)
    {
        if (rate <= 0 || rankedIds.Count == 0 || exploreCandidateIds.Count == 0)
        {
            return rankedIds;
        }

        var result = rankedIds.ToList();
        var used = result.ToHashSet();
        var explorers = exploreCandidateIds.Where(id => !used.Contains(id)).Distinct().ToList();
        if (explorers.Count == 0)
        {
            return result;
        }

        // Floor so a single-item page with rate 0.1 does not replace the only result.
        var slots = (int)Math.Floor(result.Count * rate);
        if (slots <= 0)
        {
            return result;
        }

        slots = Math.Min(slots, explorers.Count);

        // Deterministic shuffle of explorer pool from version seed.
        var rng = new Random(versionSeed);
        for (var i = explorers.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (explorers[i], explorers[j]) = (explorers[j], explorers[i]);
        }

        // Place explorers at evenly spaced indices.
        for (var s = 0; s < slots; s++)
        {
            var pos = Math.Min(result.Count - 1, (int)Math.Round((s + 0.5) * result.Count / slots));
            var exploreId = explorers[s];
            if (used.Contains(exploreId))
            {
                continue;
            }

            // Swap with position (keeps length); mark for explore reason at call site via set.
            result[pos] = exploreId;
            used.Add(exploreId);
        }

        return result;
    }

    /// <summary>Pick onboarding cover set: greedy genre coverage on popular pool titles.</summary>
    public static IReadOnlyList<int> PickOnboarding(
        IReadOnlyList<RecoOnboardingCandidate> pool,
        int count = 24)
    {
        if (pool.Count == 0 || count <= 0)
        {
            return [];
        }

        var ordered = pool
            .OrderByDescending(p => p.Popularity)
            .ThenBy(p => p.TitleId)
            .ToList();

        var selected = new List<int>(count);
        var coveredGenres = new HashSet<int>();

        // Pass 1: cover new genres.
        foreach (var c in ordered)
        {
            if (selected.Count >= count)
            {
                break;
            }

            if (c.GenreIds.Count == 0 || c.GenreIds.Any(g => !coveredGenres.Contains(g)))
            {
                selected.Add(c.TitleId);
                foreach (var g in c.GenreIds)
                {
                    coveredGenres.Add(g);
                }
            }
        }

        // Pass 2: fill with remaining popularity.
        if (selected.Count < count)
        {
            var selectedSet = selected.ToHashSet();
            foreach (var c in ordered)
            {
                if (selected.Count >= count)
                {
                    break;
                }

                if (selectedSet.Add(c.TitleId))
                {
                    selected.Add(c.TitleId);
                }
            }
        }

        return selected;
    }
}

public sealed record RecoOnboardingCandidate(
    int TitleId,
    double Popularity,
    IReadOnlyList<int> GenreIds);
