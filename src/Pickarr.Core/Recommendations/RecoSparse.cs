namespace Pickarr.Core.Recommendations;

/// <summary>Level-1 sparse TF-IDF profile and cosine similarity (§12.3).</summary>
public static class RecoSparse
{
    public const int DefaultProfileTermLimit = 200;
    public const double DimGenre = 1.0;
    public const double DimKeyword = 1.0;
    public const double DimDirector = 0.8;
    public const double DimCast = 0.5;
    public const double DimType = 0.5;
    public const double DimLanguage = 0.3;
    public const double DimCountry = 0.3;
    public const double DimDecade = 0.3;

    public const string PrefixGenre = "g:";
    public const string PrefixKeyword = "k:";
    public const string PrefixPerson = "p:";
    public const string PrefixDirector = "d:";
    public const string PrefixLanguage = "l:";
    public const string PrefixCountry = "c:";
    public const string PrefixDecade = "y:";
    public const string PrefixType = "t:";

    public static string GenreTerm(int genreId) => PrefixGenre + genreId;
    public static string KeywordTerm(int keywordId) => PrefixKeyword + keywordId;
    public static string CastTerm(int personId) => PrefixPerson + personId;
    public static string DirectorTerm(int personId) => PrefixDirector + personId;
    public static string LanguageTerm(string language) => PrefixLanguage + language;
    public static string CountryTerm(string country) => PrefixCountry + country;
    public static string DecadeTerm(int year) => PrefixDecade + DecadeOf(year);
    public static string TypeTerm(string category) => PrefixType + category;

    public static int DecadeOf(int year) => year / 10 * 10;

    /// <summary>
    /// Builds L2-normalized TF-IDF vectors for every title in the pool.
    /// IDF is computed on the pool; raw TF is binary presence × dimension weight.
    /// </summary>
    public static IReadOnlyDictionary<int, SparseVector> BuildTitleVectors(
        IReadOnlyList<RecoSparseTitleFeatures> pool)
    {
        if (pool.Count == 0)
        {
            return new Dictionary<int, SparseVector>();
        }

        var documentFrequency = new Dictionary<string, int>(StringComparer.Ordinal);
        var rawByTitle = new Dictionary<int, Dictionary<string, double>>(pool.Count);

        foreach (var title in pool)
        {
            var terms = CollectWeightedTerms(title);
            rawByTitle[title.TitleId] = terms;
            foreach (var key in terms.Keys)
            {
                documentFrequency[key] = documentFrequency.GetValueOrDefault(key) + 1;
            }
        }

        var n = pool.Count;
        var result = new Dictionary<int, SparseVector>(pool.Count);
        foreach (var title in pool)
        {
            var raw = rawByTitle[title.TitleId];
            var weighted = new Dictionary<string, double>(raw.Count, StringComparer.Ordinal);
            foreach (var (term, tf) in raw)
            {
                var df = documentFrequency[term];
                var idf = Math.Log((n + 1.0) / (df + 1.0)) + 1.0;
                weighted[term] = tf * idf;
            }

            result[title.TitleId] = SparseVector.FromWeights(weighted);
        }

        return result;
    }

    /// <summary>P = Σ s_i · x_i, L2-normalized and truncated to the top absolute weights.</summary>
    public static SparseVector BuildProfile(
        IReadOnlyDictionary<int, double> signals,
        IReadOnlyDictionary<int, SparseVector> titleVectors,
        int termLimit = DefaultProfileTermLimit)
    {
        var acc = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (titleId, signal) in signals)
        {
            if (signal == 0 || !titleVectors.TryGetValue(titleId, out var vector))
            {
                continue;
            }

            foreach (var (term, weight) in vector.Weights)
            {
                acc[term] = acc.GetValueOrDefault(term) + signal * weight;
            }
        }

        if (acc.Count == 0)
        {
            return SparseVector.Empty;
        }

        if (acc.Count > termLimit)
        {
            acc = acc
                .OrderByDescending(kv => Math.Abs(kv.Value))
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Take(termLimit)
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        }

        return SparseVector.FromWeights(acc);
    }

    public static double Cosine(SparseVector a, SparseVector b) => a.Dot(b);

    /// <summary>Sparse(t) = cos(P, x_t) for every pool title. Missing vectors score 0.</summary>
    public static IReadOnlyDictionary<int, double> ScoreAll(
        SparseVector profile,
        IReadOnlyDictionary<int, SparseVector> titleVectors)
    {
        var scores = new Dictionary<int, double>(titleVectors.Count);
        if (profile.IsEmpty)
        {
            foreach (var titleId in titleVectors.Keys)
            {
                scores[titleId] = 0;
            }

            return scores;
        }

        foreach (var (titleId, vector) in titleVectors)
        {
            scores[titleId] = Cosine(profile, vector);
        }

        return scores;
    }

    /// <summary>
    /// Top contributing profile terms present on the candidate (by |P_term|), for reason chips.
    /// </summary>
    public static IReadOnlyList<string> TopMatchingTerms(
        SparseVector profile,
        SparseVector title,
        int limit = 3)
    {
        if (profile.IsEmpty || title.IsEmpty || limit <= 0)
        {
            return [];
        }

        return profile.Weights
            .Where(kv => title.Weights.ContainsKey(kv.Key) && kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(limit)
            .Select(kv => kv.Key)
            .ToList();
    }

    /// <summary>Nearest titles by sparse cosine (used for similarTo reasons and similar API).</summary>
    public static IReadOnlyList<(int TitleId, double Similarity)> TopSimilar(
        SparseVector query,
        IReadOnlyDictionary<int, SparseVector> candidates,
        int limit,
        double minSimilarity = 0,
        IReadOnlySet<int>? excludeTitleIds = null)
    {
        if (query.IsEmpty || limit <= 0)
        {
            return [];
        }

        var scored = new List<(int TitleId, double Similarity)>();
        foreach (var (titleId, vector) in candidates)
        {
            if (excludeTitleIds is not null && excludeTitleIds.Contains(titleId))
            {
                continue;
            }

            var sim = Cosine(query, vector);
            if (sim < minSimilarity)
            {
                continue;
            }

            scored.Add((titleId, sim));
        }

        return scored
            .OrderByDescending(s => s.Similarity)
            .ThenBy(s => s.TitleId)
            .Take(limit)
            .ToList();
    }

    private static Dictionary<string, double> CollectWeightedTerms(RecoSparseTitleFeatures title)
    {
        var terms = new Dictionary<string, double>(StringComparer.Ordinal);

        void Add(string term, double dim)
        {
            if (string.IsNullOrWhiteSpace(term) || dim <= 0)
            {
                return;
            }

            terms[term] = Math.Max(terms.GetValueOrDefault(term), dim);
        }

        foreach (var genreId in title.GenreIds)
        {
            Add(GenreTerm(genreId), DimGenre);
        }

        foreach (var keywordId in title.KeywordIds)
        {
            Add(KeywordTerm(keywordId), DimKeyword);
        }

        foreach (var personId in title.CastPersonIds)
        {
            Add(CastTerm(personId), DimCast);
        }

        foreach (var personId in title.DirectorPersonIds)
        {
            Add(DirectorTerm(personId), DimDirector);
        }

        if (!string.IsNullOrWhiteSpace(title.OriginalLanguage))
        {
            Add(LanguageTerm(title.OriginalLanguage), DimLanguage);
        }

        foreach (var country in title.OriginCountries)
        {
            if (!string.IsNullOrWhiteSpace(country))
            {
                Add(CountryTerm(country.Trim()), DimCountry);
            }
        }

        if (title.ReleaseYear is { } year and > 0)
        {
            Add(DecadeTerm(year), DimDecade);
        }

        if (!string.IsNullOrWhiteSpace(title.Category))
        {
            Add(TypeTerm(title.Category), DimType);
        }

        return terms;
    }
}

public sealed record RecoSparseTitleFeatures(
    int TitleId,
    string Category,
    string? OriginalLanguage,
    int? ReleaseYear,
    IReadOnlyList<int> GenreIds,
    IReadOnlyList<int> KeywordIds,
    IReadOnlyList<int> CastPersonIds,
    IReadOnlyList<int> DirectorPersonIds,
    IReadOnlyList<string> OriginCountries);

/// <summary>L2-normalized sparse bag of term → weight.</summary>
public sealed class SparseVector
{
    public static SparseVector Empty { get; } = new(new Dictionary<string, double>(StringComparer.Ordinal));

    private SparseVector(IReadOnlyDictionary<string, double> weights)
    {
        Weights = weights;
    }

    public IReadOnlyDictionary<string, double> Weights { get; }

    public bool IsEmpty => Weights.Count == 0;

    public static SparseVector FromWeights(IReadOnlyDictionary<string, double> raw)
    {
        if (raw.Count == 0)
        {
            return Empty;
        }

        var norm = Math.Sqrt(raw.Values.Sum(v => v * v));
        if (norm < double.Epsilon)
        {
            return Empty;
        }

        var normalized = new Dictionary<string, double>(raw.Count, StringComparer.Ordinal);
        foreach (var (term, weight) in raw)
        {
            normalized[term] = weight / norm;
        }

        return new SparseVector(normalized);
    }

    public double Dot(SparseVector other)
    {
        if (IsEmpty || other.IsEmpty)
        {
            return 0;
        }

        // Iterate the smaller side.
        var a = Weights;
        var b = other.Weights;
        if (a.Count > b.Count)
        {
            (a, b) = (b, a);
        }

        var sum = 0.0;
        foreach (var (term, weight) in a)
        {
            if (b.TryGetValue(term, out var otherWeight))
            {
                sum += weight * otherWeight;
            }
        }

        return sum;
    }
}
