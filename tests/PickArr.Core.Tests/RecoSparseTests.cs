using Pickarr.Core.Recommendations;
using Shouldly;

namespace Pickarr.Core.Tests;

public sealed class RecoSparseTests
{
    [Fact]
    public void Sci_fi_likes_rank_other_sci_fi_above_drama()
    {
        var pool = new[]
        {
            Features(1, genres: [878], category: "movie"), // liked sci-fi
            Features(2, genres: [878], category: "movie"), // other sci-fi
            Features(3, genres: [18], category: "movie"),  // drama
            Features(4, genres: [878, 28], category: "movie"),
        };

        var vectors = RecoSparse.BuildTitleVectors(pool);
        var signals = new Dictionary<int, double> { [1] = 1.0 };
        var profile = RecoSparse.BuildProfile(signals, vectors);
        var scores = RecoSparse.ScoreAll(profile, vectors);

        scores[2].ShouldBeGreaterThan(scores[3]);
        scores[4].ShouldBeGreaterThan(scores[3]);
        scores[1].ShouldBeGreaterThan(0.5);
    }

    [Fact]
    public void Dislike_lowers_neighbours_sharing_terms()
    {
        var pool = new[]
        {
            Features(1, genres: [878], category: "movie"),
            Features(2, genres: [878], category: "movie"),
            Features(3, genres: [18], category: "movie"),
        };

        var vectors = RecoSparse.BuildTitleVectors(pool);
        var likeOnly = RecoSparse.ScoreAll(
            RecoSparse.BuildProfile(new Dictionary<int, double> { [1] = 1.0 }, vectors),
            vectors);
        var withDislike = RecoSparse.ScoreAll(
            RecoSparse.BuildProfile(new Dictionary<int, double> { [1] = 1.0, [2] = -1.0 }, vectors),
            vectors);

        // Title 2 shares the disliked neighbour's terms → sparse score drops vs like-only profile.
        withDislike[2].ShouldBeLessThan(likeOnly[2]);
    }

    [Fact]
    public void TopSimilar_excludes_ids_and_respects_min_similarity()
    {
        var pool = new[]
        {
            Features(1, genres: [878], language: "en", category: "movie"),
            Features(2, genres: [878], language: "en", category: "movie"),
            Features(3, genres: [18], language: "fr", category: "series"),
        };
        var vectors = RecoSparse.BuildTitleVectors(pool);
        var similar = RecoSparse.TopSimilar(
            vectors[1],
            vectors,
            limit: 5,
            minSimilarity: 0.01,
            excludeTitleIds: new HashSet<int> { 1 });

        similar.ShouldNotContain(s => s.TitleId == 1);
        similar[0].TitleId.ShouldBe(2);
        similar[0].Similarity.ShouldBeGreaterThan(similar.Last().Similarity);
    }

    [Fact]
    public void Profile_truncates_to_term_limit()
    {
        var genres = Enumerable.Range(1, 50).ToArray();
        var pool = genres.Select(g => Features(g, genres: [g], category: "movie")).ToList();
        var vectors = RecoSparse.BuildTitleVectors(pool);
        var signals = genres.ToDictionary(g => g, _ => 1.0);
        var profile = RecoSparse.BuildProfile(signals, vectors, termLimit: 10);

        profile.Weights.Count.ShouldBeLessThanOrEqualTo(10);
    }

    private static RecoSparseTitleFeatures Features(
        int id,
        int[]? genres = null,
        int[]? keywords = null,
        string category = "movie",
        string? language = "en",
        int? year = 2020) =>
        new(
            id,
            category,
            language,
            year,
            genres ?? [],
            keywords ?? [],
            CastPersonIds: [],
            DirectorPersonIds: [],
            OriginCountries: []);
}
