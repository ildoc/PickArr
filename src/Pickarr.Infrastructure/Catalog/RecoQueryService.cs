using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Arr;
using Pickarr.Core.Catalog;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Metadata;
using Pickarr.Core.Recommendations;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Metadata.Reco;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Catalog;

public sealed class RecoQueryService(
    PickarrDbContext db,
    ISettingsStore settings,
    IClock clock) : IRecoQueryService
{
    public async Task<RecommendationsPageDto> QueryAsync(
        RecommendationsQuery query,
        CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);

        var scope = RecoMembership.NormalizeScope(query.Scope);
        if (scope == RecoMembership.ScopeSource && query.SourceId is null)
        {
            throw new ArgumentException("sourceId is required when scope=source.", nameof(query));
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 48 : query.PageSize, 1, 100);
        var personalProfileId = query.ProfileId;

        var state = personalProfileId is int tasteProfileId
            ? await RecoProfileStates.FindNoTrackingAsync(db, tasteProfileId, cancellationToken)
            : null;
        var version = state?.CurrentVersion ?? 0;
        var stage = state?.Stage ?? RecoLearning.StageCold;

        if (version <= 0)
        {
            return new RecommendationsPageDto(version, stage, page, pageSize, 0, []);
        }

        var enabledSourceIds = await LoadEnabledSourceIdsAsync(cancellationToken);
        var sourceId = query.SourceId;

        // Membership sets in memory — avoids correlated EXISTS on every ranked row (NFR-REC-01).
        HashSet<int> memberIds;
        if (scope == RecoMembership.ScopeSource)
        {
            var sid = sourceId!.Value;
            memberIds = (await db.Availabilities.AsNoTracking()
                    .Where(a => a.CatalogSourceId == sid)
                    .Select(a => a.TitleId)
                    .Distinct()
                    .ToListAsync(cancellationToken))
                .ToHashSet();
        }
        else
        {
            memberIds = (await db.Availabilities.AsNoTracking()
                    .Where(a => enabledSourceIds.Contains(a.CatalogSourceId))
                    .Select(a => a.TitleId)
                    .Distinct()
                    .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        var ratedQuery = db.Ratings.AsNoTracking();
        var watchedQuery = db.Watched.AsNoTracking();
        if (personalProfileId is int signalProfileId)
        {
            ratedQuery = ratedQuery.Where(r => r.ProfileId == signalProfileId);
            watchedQuery = watchedQuery.Where(w => w.ProfileId == signalProfileId);
        }

        var ratedIds = (await ratedQuery
                .Select(r => r.TitleId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var watchedIds = (await watchedQuery
                .Select(w => w.TitleId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var excludeInLibrary = !query.IncludeInLibrary
            && await ExcludesLibraryAsync(personalProfileId, cancellationToken);
        HashSet<int>? libraryIds = null;
        if (excludeInLibrary)
        {
            libraryIds = (await db.ArrLibraryItems.AsNoTracking()
                    .Where(a => a.TitleId != null)
                    .Select(a => a.TitleId!.Value)
                    .Distinct()
                    .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        HashSet<int>? titleFilterIds = null;
        var needsTitleFilters = (query.Types is { Count: > 0 })
            || (query.Genres is { Count: > 0 })
            || query.YearFrom is not null
            || query.YearTo is not null;
        if (needsTitleFilters)
        {
            var titleQuery = db.Titles.AsNoTracking().AsQueryable();
            if (query.Types is { Count: > 0 })
            {
                var categories = query.Types
                    .Select(MapTypeToCategory)
                    .Where(c => c is not null)
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (categories.Count > 0)
                {
                    titleQuery = titleQuery.Where(t => categories.Contains(t.Category));
                }
            }

            if (query.Genres is { Count: > 0 })
            {
                var genreIds = query.Genres.ToList();
                titleQuery = titleQuery.Where(t =>
                    db.TitleGenres.Any(tg => tg.TitleId == t.Id && genreIds.Contains(tg.GenreId)));
            }

            if (query.YearFrom is { } yearFrom)
            {
                titleQuery = titleQuery.Where(t => t.ReleaseYear >= yearFrom);
            }

            if (query.YearTo is { } yearTo)
            {
                titleQuery = titleQuery.Where(t => t.ReleaseYear <= yearTo);
            }

            titleFilterIds = (await titleQuery.Select(t => t.Id).ToListAsync(cancellationToken)).ToHashSet();
        }

        bool IsEligible(int titleId) =>
            memberIds.Contains(titleId)
            && !ratedIds.Contains(titleId)
            && !watchedIds.Contains(titleId)
            && (libraryIds is null || !libraryIds.Contains(titleId))
            && (titleFilterIds is null || titleFilterIds.Contains(titleId));

        var exploreRate = await GetDoubleSettingAsync(SettingsKeys.RecoExploreRate, RecoExplore.DefaultRate, cancellationToken);
        var lambda = query.Diversify
            ? await GetDoubleSettingAsync(SettingsKeys.RecoMmrLambda, RecoMmr.DefaultLambda, cancellationToken)
            : 0;
        var useMmr = query.Diversify && lambda > 0;

        var pageEnd = page * pageSize;
        var take = useMmr || exploreRate > 0
            ? Math.Max(RecoMmr.DefaultCandidateCap, pageEnd)
            : pageEnd;

        // Light scan (no ReasonsJson) — payload dominates at 5k (NFR-REC-01).
        var ranksQuery = db.Recommendations.AsNoTracking().Where(r => r.Version == version);
        if (personalProfileId is int rankProfileId)
        {
            ranksQuery = ranksQuery.Where(r => r.ProfileId == rankProfileId);
        }

        var allRanks = await ranksQuery
            .OrderBy(r => r.Rank)
            .Select(r => new { r.TitleId, r.Score, r.Rank })
            .ToListAsync(cancellationToken);

        var total = 0;
        var ranked = new List<(int TitleId, double Score, int Rank)>(Math.Min(take, allRanks.Count));
        foreach (var row in allRanks)
        {
            if (!IsEligible(row.TitleId))
            {
                continue;
            }

            total++;
            if (ranked.Count < take)
            {
                ranked.Add((row.TitleId, row.Score, row.Rank));
            }
        }

        if (total == 0 || ranked.Count == 0)
        {
            return new RecommendationsPageDto(version, stage, page, pageSize, total, []);
        }

        var orderedIds = ranked.Select(r => r.TitleId).ToList();
        if (useMmr)
        {
            var mmrPoolIds = orderedIds.Take(RecoMmr.DefaultCandidateCap).ToList();
            var sim = await BuildSimilarityLookupAsync(mmrPoolIds, cancellationToken);
            orderedIds = RecoMmr.RerankIds(
                ranked.Select(r => (r.TitleId, r.Score)).ToList(),
                (a, b) => sim.TryGetValue((Math.Min(a, b), Math.Max(a, b)), out var s) ? s : 0,
                lambda).ToList();
        }

        if (exploreRate > 0)
        {
            var exploreCandidates = await PickExploreCandidatesAsync(
                orderedIds,
                IsEligible,
                personalProfileId,
                cancellationToken);
            orderedIds = RecoExplore.Inject(orderedIds, exploreCandidates, exploreRate, version)
                .Where(IsEligible)
                .ToList();
        }

        var pageIds = orderedIds
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        if (pageIds.Count == 0)
        {
            return new RecommendationsPageDto(version, stage, page, pageSize, total, []);
        }

        var pageQuery = db.Recommendations.AsNoTracking()
            .Where(r => r.Version == version && pageIds.Contains(r.TitleId));
        if (personalProfileId is int pageProfileId)
        {
            pageQuery = pageQuery.Where(r => r.ProfileId == pageProfileId);
        }

        var pageRecRows = await pageQuery
            .Select(r => new { r.TitleId, r.Score, r.Rank, r.ReasonsJson, r.Personal, r.Prior })
            .ToListAsync(cancellationToken);
        var recById = pageRecRows.ToDictionary(r => r.TitleId);
        var titleIds = pageIds;
        var languageSourceId = sourceId
            ?? enabledSourceIds.OrderBy(id => id).FirstOrDefault();

        CatalogSource? languageSource = null;
        if (languageSourceId > 0)
        {
            languageSource = await db.CatalogSources.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == languageSourceId, cancellationToken);
        }

        var language = languageSource?.MetadataLanguage ?? LanguageFallback.English;
        var newWindow = await GetIntSettingAsync(SettingsKeys.CatalogNewWindowDays, 14, cancellationToken);
        var newCutoff = clock.UtcNow.AddDays(-newWindow);

        var titles = await db.Titles.AsNoTracking()
            .Include(t => t.Localizations)
            .Include(t => t.TitleGenres).ThenInclude(tg => tg.Genre)!.ThenInclude(g => g!.Localizations)
            .Include(t => t.Favorites)
            .Include(t => t.WatchedItems)
            .Include(t => t.Ratings)
            .Include(t => t.Availabilities).ThenInclude(a => a.CatalogSource)!.ThenInclude(s => s!.Provider)
            .Where(t => titleIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        var titlesById = titles.ToDictionary(t => t.Id);
        var arrItems = await db.ArrLibraryItems.AsNoTracking()
            .Where(a => a.TitleId != null && titleIds.Contains(a.TitleId.Value))
            .ToListAsync(cancellationToken);
        var arrByTitle = arrItems
            .Where(a => a.TitleId is not null)
            .GroupBy(a => a.TitleId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var items = new List<RecommendationItemDto>(pageIds.Count);
        foreach (var id in pageIds)
        {
            if (!titlesById.TryGetValue(id, out var title))
            {
                continue;
            }

            arrByTitle.TryGetValue(title.Id, out var arrItem);
            Recommendation rec;
            if (recById.TryGetValue(id, out var rankedRow))
            {
                rec = new Recommendation
                {
                    Version = version,
                    TitleId = id,
                    Score = rankedRow.Score,
                    Personal = rankedRow.Personal,
                    Prior = rankedRow.Prior,
                    Rank = rankedRow.Rank,
                    ReasonsJson = rankedRow.ReasonsJson,
                };
            }
            else
            {
                // Explore may inject eligible titles that were not in the top filtered rank slice.
                rec = new Recommendation
                {
                    Version = version,
                    TitleId = id,
                    Score = 0,
                    Rank = 0,
                    ReasonsJson = RecoReasons.Serialize([new RecoReasonDto(RecoReasons.Explore)]),
                };
            }

            items.Add(MapItem(
                title,
                rec,
                scope,
                sourceId,
                enabledSourceIds,
                language,
                newCutoff,
                arrItem,
                personalProfileId));
        }

        return new RecommendationsPageDto(version, stage, page, pageSize, total, items);
    }

    /// <summary>
    /// Prefer same-genre neighbours when available; otherwise the enabled pool.
    /// Caps brute-force cosine work for large catalogs (NFR-REC-01 / SP-R3).
    /// </summary>
    private async Task<HashSet<int>> NarrowSimilarCandidatesAsync(
        int queryTitleId,
        HashSet<int> enabledPoolSet,
        HashSet<int> exclude,
        CancellationToken cancellationToken)
    {
        const int maxCandidates = 120;
        var queryGenreIds = await db.TitleGenres.AsNoTracking()
            .Where(tg => tg.TitleId == queryTitleId)
            .Select(tg => tg.GenreId)
            .ToListAsync(cancellationToken);

        List<int> rawIds;
        if (queryGenreIds.Count > 0)
        {
            // Load co-genre ids without embedding enabledPool into SQL IN (NFR-REC-01).
            rawIds = await db.TitleGenres.AsNoTracking()
                .Where(tg => queryGenreIds.Contains(tg.GenreId))
                .Select(tg => tg.TitleId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }
        else
        {
            rawIds = enabledPoolSet.ToList();
        }

        var narrowed = new List<int>(Math.Min(maxCandidates, rawIds.Count));
        foreach (var id in rawIds)
        {
            if (id == queryTitleId || exclude.Contains(id) || !enabledPoolSet.Contains(id))
            {
                continue;
            }

            narrowed.Add(id);
            if (narrowed.Count >= maxCandidates)
            {
                break;
            }
        }

        if (narrowed.Count >= 20 || queryGenreIds.Count == 0)
        {
            return narrowed.ToHashSet();
        }

        // Genre filter too tight — fall back to a capped enabled pool in memory.
        narrowed.Clear();
        foreach (var id in enabledPoolSet)
        {
            if (exclude.Contains(id))
            {
                continue;
            }

            narrowed.Add(id);
            if (narrowed.Count >= maxCandidates)
            {
                break;
            }
        }

        return narrowed.ToHashSet();
    }

    private async Task<IReadOnlyList<(int TitleId, double Similarity)>> QuerySimilarSparseAsync(
        int queryTitleId,
        HashSet<int> enabledPoolSet,
        HashSet<int> exclude,
        int limit,
        CancellationToken cancellationToken)
    {
        var candidateIds = await NarrowSimilarCandidatesAsync(
            queryTitleId,
            enabledPoolSet,
            exclude,
            cancellationToken);
        var poolIdSet = new HashSet<int>(candidateIds) { queryTitleId };

        var featureTitles = await db.Titles.AsNoTracking()
            .Where(t => poolIdSet.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                t.Category,
                t.OriginalLanguage,
                t.ReleaseYear,
                t.OriginCountries,
            })
            .ToListAsync(cancellationToken);

        var genreRows = await db.TitleGenres.AsNoTracking()
            .Where(tg => poolIdSet.Contains(tg.TitleId))
            .Select(tg => new { tg.TitleId, tg.GenreId })
            .ToListAsync(cancellationToken);
        var genresByTitle = genreRows.GroupBy(g => g.TitleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(x => x.GenreId).ToList());

        var keywordRows = await db.TitleKeywords.AsNoTracking()
            .Where(tk => poolIdSet.Contains(tk.TitleId))
            .Select(tk => new { tk.TitleId, tk.KeywordId })
            .ToListAsync(cancellationToken);
        var keywordsByTitle = keywordRows.GroupBy(k => k.TitleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(x => x.KeywordId).ToList());

        var creditRows = await db.TitleCredits.AsNoTracking()
            .Where(tc => poolIdSet.Contains(tc.TitleId))
            .Select(tc => new { tc.TitleId, tc.PersonId, tc.Role })
            .ToListAsync(cancellationToken);
        var castByTitle = creditRows.Where(c => c.Role == CreditRoles.Cast)
            .GroupBy(c => c.TitleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(x => x.PersonId).Distinct().ToList());
        var directorByTitle = creditRows
            .Where(c => c.Role == CreditRoles.Director || c.Role == CreditRoles.Creator)
            .GroupBy(c => c.TitleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(x => x.PersonId).Distinct().ToList());

        static IReadOnlyList<string> ParseCountries(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        var features = featureTitles.Select(t => new RecoSparseTitleFeatures(
            t.Id,
            t.Category,
            t.OriginalLanguage,
            t.ReleaseYear,
            genresByTitle.GetValueOrDefault(t.Id) ?? [],
            keywordsByTitle.GetValueOrDefault(t.Id) ?? [],
            castByTitle.GetValueOrDefault(t.Id) ?? [],
            directorByTitle.GetValueOrDefault(t.Id) ?? [],
            ParseCountries(t.OriginCountries))).ToList();

        var vectors = RecoSparse.BuildTitleVectors(features);
        if (!vectors.TryGetValue(queryTitleId, out var queryVector) || queryVector.IsEmpty)
        {
            return [];
        }

        var enabledPoolVectors = vectors
            .Where(kv => enabledPoolSet.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        return RecoSparse.TopSimilar(
            queryVector,
            enabledPoolVectors,
            limit,
            minSimilarity: 0.01,
            excludeTitleIds: exclude);
    }

    private async Task<Dictionary<(int, int), double>> BuildSimilarityLookupAsync(
        IReadOnlyList<int> titleIds,
        CancellationToken cancellationToken)
    {
        var sim = new Dictionary<(int, int), double>();
        if (titleIds.Count == 0)
        {
            return sim;
        }

        // Embedding pairwise is O(n²·dim); beyond a small cap prefer genre Jaccard
        // (FR-REC-RC-10 / §12.8 allows sparse sim; NFR-REC-01).
        const int embeddingPairwiseCap = 48;
        if (titleIds.Count <= embeddingPairwiseCap)
        {
            var modelState = await RecoProfileStates.FindAdminNoTrackingAsync(db, cancellationToken);
            var modelId = modelState?.ModelId ?? RecoModelCatalog.DefaultModelId;
            var emb = await db.TitleEmbeddings.AsNoTracking()
                .Where(e => e.ModelId == modelId && titleIds.Contains(e.TitleId))
                .Select(e => new { e.TitleId, e.Vector })
                .ToListAsync(cancellationToken);

            if (emb.Count >= 2)
            {
                var map = emb.ToDictionary(e => e.TitleId, e => RecoEmbeddingService.BytesToFloats(e.Vector));
                var ids = map.Keys.ToList();
                for (var i = 0; i < ids.Count; i++)
                {
                    for (var j = i + 1; j < ids.Count; j++)
                    {
                        var a = ids[i];
                        var b = ids[j];
                        sim[(Math.Min(a, b), Math.Max(a, b))] = RecoKnn.Dot(map[a], map[b]);
                    }
                }

                return sim;
            }
        }

        // Genre Jaccard — cheap diversity signal for MMR on the first-200 cap.
        var genres = await db.TitleGenres.AsNoTracking()
            .Where(tg => titleIds.Contains(tg.TitleId))
            .Select(tg => new { tg.TitleId, tg.GenreId })
            .ToListAsync(cancellationToken);
        var byTitle = genres.GroupBy(g => g.TitleId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.GenreId).ToHashSet());
        var keys = byTitle.Keys.ToList();
        for (var i = 0; i < keys.Count; i++)
        {
            for (var j = i + 1; j < keys.Count; j++)
            {
                var a = keys[i];
                var b = keys[j];
                var ga = byTitle[a];
                var gb = byTitle[b];
                var inter = 0;
                foreach (var g in ga)
                {
                    if (gb.Contains(g))
                    {
                        inter++;
                    }
                }

                var union = ga.Count + gb.Count - inter;
                sim[(Math.Min(a, b), Math.Max(a, b))] = union == 0 ? 0 : (double)inter / union;
            }
        }

        return sim;
    }

    private async Task<IReadOnlyList<int>> PickExploreCandidatesAsync(
        IReadOnlyList<int> rankedIds,
        Func<int, bool> isEligible,
        int? personalProfileId,
        CancellationToken cancellationToken)
    {
        // Mid-high prior titles not already near the top; prefer unseen genres vs profile.
        // Candidates must still pass IsEligible so explore never reintroduces rated/watched titles.
        var top = rankedIds.Take(40).ToHashSet();
        var state = personalProfileId is int exploreTasteId
            ? await RecoProfileStates.FindNoTrackingAsync(db, exploreTasteId, cancellationToken)
            : null;
        var profileGenres = new HashSet<int>();
        if (!string.IsNullOrWhiteSpace(state?.ProfileJson))
        {
            try
            {
                var weights = JsonSerializer.Deserialize<Dictionary<string, double>>(state.ProfileJson!);
                if (weights is not null)
                {
                    foreach (var key in weights.Keys)
                    {
                        if (key.StartsWith(RecoSparse.PrefixGenre, StringComparison.Ordinal)
                            && int.TryParse(key[RecoSparse.PrefixGenre.Length..], out var gid)
                            && weights[key] > 0)
                        {
                            profileGenres.Add(gid);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // ignore
            }
        }

        var exploreVersion = state == null ? 0 : state.CurrentVersion;
        var exploreQuery = db.Recommendations.AsNoTracking().Where(r => r.Version == exploreVersion);
        if (personalProfileId is int exploreProfileId)
        {
            exploreQuery = exploreQuery.Where(r => r.ProfileId == exploreProfileId);
        }

        var candidates = await exploreQuery
            .OrderByDescending(r => r.Prior)
            .Take(200)
            .Select(r => r.TitleId)
            .ToListAsync(cancellationToken);

        var eligible = candidates.Where(id => !top.Contains(id) && isEligible(id)).ToList();
        if (profileGenres.Count == 0)
        {
            return eligible.Take(40).ToList();
        }

        var genreRows = await db.TitleGenres.AsNoTracking()
            .Where(tg => eligible.Contains(tg.TitleId))
            .Select(tg => new { tg.TitleId, tg.GenreId })
            .ToListAsync(cancellationToken);
        var genresByTitle = genreRows.GroupBy(g => g.TitleId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.GenreId).ToHashSet());

        return eligible
            .Where(id =>
                !genresByTitle.TryGetValue(id, out var gs)
                || gs.All(g => !profileGenres.Contains(g))
                || gs.Any(g => !profileGenres.Contains(g)))
            .Take(40)
            .ToList();
    }

    public async Task<RecommendationsPageDto> GetOnboardingAsync(
        int? sourceId = null,
        IReadOnlyList<int>? excludeTitleIds = null,
        CancellationToken cancellationToken = default,
        int? profileId = null)
    {
        await EnsureEnabledAsync(cancellationToken);
        var enabledSourceIds = await LoadEnabledSourceIdsAsync(cancellationToken);
        if (enabledSourceIds.Count == 0)
        {
            return new RecommendationsPageDto(0, RecoLearning.StageCold, 1, 24, 0, []);
        }

        var poolIds = await db.Availabilities.AsNoTracking()
            .Where(a => enabledSourceIds.Contains(a.CatalogSourceId))
            .Select(a => a.TitleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var ratedQuery = db.Ratings.AsNoTracking().AsQueryable();
        var watchedSkipQuery = db.Watched.AsNoTracking().AsQueryable();
        if (profileId is int onboardingProfileId)
        {
            ratedQuery = ratedQuery.Where(r => r.ProfileId == onboardingProfileId);
            watchedSkipQuery = watchedSkipQuery.Where(w => w.ProfileId == onboardingProfileId);
        }

        var rated = await ratedQuery.Select(r => r.TitleId).ToListAsync(cancellationToken);
        var skipSet = rated.ToHashSet();
        foreach (var id in await watchedSkipQuery.Select(w => w.TitleId).ToListAsync(cancellationToken))
        {
            skipSet.Add(id);
        }

        if (excludeTitleIds is { Count: > 0 })
        {
            foreach (var id in excludeTitleIds)
            {
                skipSet.Add(id);
            }
        }

        var candidates = await db.Titles.AsNoTracking()
            .Where(t => poolIds.Contains(t.Id) && !skipSet.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                Popularity = t.Popularity ?? 0,
            })
            .ToListAsync(cancellationToken);

        var genreRows = await db.TitleGenres.AsNoTracking()
            .Where(tg => candidates.Select(c => c.Id).Contains(tg.TitleId))
            .Select(tg => new { tg.TitleId, tg.GenreId })
            .ToListAsync(cancellationToken);
        var genresByTitle = genreRows.GroupBy(g => g.TitleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(x => x.GenreId).ToList());

        var picked = RecoExplore.PickOnboarding(
            candidates.Select(c => new RecoOnboardingCandidate(
                c.Id,
                c.Popularity,
                genresByTitle.GetValueOrDefault(c.Id) ?? [])).ToList(),
            24);

        if (picked.Count == 0)
        {
            return new RecommendationsPageDto(0, RecoLearning.StageCold, 1, 24, 0, []);
        }

        var languageSourceId = sourceId ?? enabledSourceIds.OrderBy(id => id).FirstOrDefault();
        CatalogSource? languageSource = null;
        if (languageSourceId > 0)
        {
            languageSource = await db.CatalogSources.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == languageSourceId, cancellationToken);
        }

        var language = languageSource?.MetadataLanguage ?? LanguageFallback.English;
        var newWindow = await GetIntSettingAsync(SettingsKeys.CatalogNewWindowDays, 14, cancellationToken);
        var newCutoff = clock.UtcNow.AddDays(-newWindow);

        var titles = await db.Titles.AsNoTracking()
            .Include(t => t.Localizations)
            .Include(t => t.TitleGenres).ThenInclude(tg => tg.Genre)!.ThenInclude(g => g!.Localizations)
            .Include(t => t.Favorites)
            .Include(t => t.WatchedItems)
            .Include(t => t.Ratings)
            .Include(t => t.Availabilities).ThenInclude(a => a.CatalogSource)!.ThenInclude(s => s!.Provider)
            .Where(t => picked.Contains(t.Id))
            .ToListAsync(cancellationToken);
        var titlesById = titles.ToDictionary(t => t.Id);

        var items = new List<RecommendationItemDto>();
        foreach (var id in picked)
        {
            if (!titlesById.TryGetValue(id, out var title))
            {
                continue;
            }

            var fake = new Recommendation
            {
                TitleId = id,
                Score = title.Popularity ?? 0,
                ReasonsJson = "[]",
            };
            items.Add(MapItem(
                title,
                fake,
                RecoMembership.ScopeEnabled,
                sourceId,
                enabledSourceIds,
                language,
                newCutoff,
                arrItem: null,
                personalProfileId: profileId));
        }

        return new RecommendationsPageDto(0, RecoLearning.StageCold, 1, 24, items.Count, items);
    }

    public async Task<RecoStatusDto> GetStatusAsync(
        CancellationToken cancellationToken = default,
        int? profileId = null)
    {
        var enabled = await GetBoolSettingAsync(SettingsKeys.RecoEnabled, true, cancellationToken);
        var n0 = await GetDoubleSettingAsync(SettingsKeys.RecoN0, RecoLearning.DefaultN0, cancellationToken);
        var modelState = await RecoProfileStates.FindAdminNoTrackingAsync(db, cancellationToken);
        var state = profileId is int tasteProfileId
            ? await RecoProfileStates.FindNoTrackingAsync(db, tasteProfileId, cancellationToken)
            : modelState;

        var enabledSourceIds = await LoadEnabledSourceIdsAsync(cancellationToken);
        var poolCount = enabledSourceIds.Count == 0
            ? 0
            : await db.Availabilities.AsNoTracking()
                .Where(a => enabledSourceIds.Contains(a.CatalogSourceId))
                .Select(a => a.TitleId)
                .Distinct()
                .CountAsync(cancellationToken);

        var effectiveN = state?.EffectiveN ?? 0;
        var stage = state?.Stage ?? RecoLearning.StageCold;
        var ratingCount = state?.RatingCount
            ?? (profileId is int countedProfileId
                ? await db.Ratings.CountAsync(r => r.ProfileId == countedProfileId, cancellationToken)
                : await db.Ratings.CountAsync(cancellationToken));

        return new RecoStatusDto(
            Enabled: enabled,
            Stage: stage,
            RatingCount: ratingCount,
            EffectiveN: effectiveN,
            TargetN: n0,
            PersonalWeight: RecoLearning.PersonalWeight(effectiveN, n0),
            Model: new RecoModelStatusDto(
                modelState?.ModelStatus ?? "Absent",
                modelState?.ModelId,
                SizeBytes: null),
            Embeddings: new RecoEmbeddingsStatusDto(
                modelState?.EmbeddingCoverage ?? 0,
                Count: 0,
                Pending: 0),
            Pool: new RecoPoolStatusDto(poolCount),
            LastBuiltAt: state?.BuiltAt,
            Version: state?.CurrentVersion ?? 0,
            LastError: state?.LastError);
    }

    private RecommendationItemDto MapItem(
        Title title,
        Recommendation rec,
        string scope,
        int? sourceId,
        HashSet<int> enabledSourceIds,
        string language,
        DateTime newCutoff,
        ArrLibraryItem? arrItem,
        int? personalProfileId = null)
    {
        var (name, _) = ResolveLocalization(title, language);
        AvailabilitySummaryDto? availability = null;
        IReadOnlyList<CatalogPlaceDto> places;

        if (scope == RecoMembership.ScopeSource && sourceId is { } sid)
        {
            var av = title.Availabilities.FirstOrDefault(a => a.CatalogSourceId == sid);
            if (av is not null)
            {
                var isNew = av.RemovedAt is null
                            && !av.IsBaseline
                            && av.AddedAt is { } added
                            && added >= newCutoff;
                availability = new AvailabilitySummaryDto(av.AddedAt, av.RemovedAt, av.LeavingDate, isNew);
            }

            places = title.Availabilities
                .Where(a => a.CatalogSourceId == sid && a.CatalogSource is not null)
                .Select(a => new CatalogPlaceDto(
                    a.CatalogSource!.Provider?.DisplayName ?? "",
                    a.CatalogSource.CountryCode,
                    a.RemovedAt))
                .ToList();
        }
        else
        {
            places = title.Availabilities
                .Where(a => enabledSourceIds.Contains(a.CatalogSourceId) && a.CatalogSource is not null)
                .OrderBy(a => a.CatalogSource!.Provider?.DisplayName)
                .ThenBy(a => a.CatalogSource!.CountryCode)
                .Select(a => new CatalogPlaceDto(
                    a.CatalogSource!.Provider?.DisplayName ?? "",
                    a.CatalogSource.CountryCode,
                    a.RemovedAt))
                .ToList();
        }

        return new RecommendationItemDto(
            title.Id,
            name,
            title.OriginalTitle,
            title.ReleaseYear,
            ToApiCategory(title.Category),
            TmdbImageUrls.Build(title.PosterPath, "w342"),
            MapGenres(title, language),
            title.VoteTmdb,
            RatingImdb: null,
            personalProfileId is int favoriteProfileId && title.Favorites.Any(f => f.ProfileId == favoriteProfileId),
            personalProfileId is int watchedProfileId && title.WatchedItems.Any(w => w.ProfileId == watchedProfileId),
            personalProfileId is int ratedProfileId
                ? RatingValues.ToApiOrNull(title.Ratings.FirstOrDefault(r => r.ProfileId == ratedProfileId)?.Value)
                : null,
            availability,
            BuildArrSummary(title.MediaType, arrItem),
            places,
            new RecoItemScoreDto(
                rec.Score,
                Personalized: rec.Personal is not null,
                ParseReasons(rec.ReasonsJson),
                Similarity: null));
    }

    public async Task<SimilarTitlesDto> QuerySimilarAsync(
        SimilarTitlesQuery query,
        CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);

        var limit = Math.Clamp(query.Limit <= 0 ? 20 : query.Limit, 1, 50);
        var exists = await db.Titles.AsNoTracking().AnyAsync(t => t.Id == query.TitleId, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException($"Title {query.TitleId} not found.");
        }

        var enabledSourceIds = await LoadEnabledSourceIdsAsync(cancellationToken);
        if (enabledSourceIds.Count == 0)
        {
            return new SimilarTitlesDto([]);
        }

        var enabledPoolIds = await db.Availabilities.AsNoTracking()
            .Where(a => enabledSourceIds.Contains(a.CatalogSourceId))
            .Select(a => a.TitleId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var enabledPoolSet = enabledPoolIds.ToHashSet();

        var exclude = new HashSet<int>();
        if (query.ProfileId is int similarProfileId)
        {
            foreach (var id in await db.Ratings.AsNoTracking()
                         .Where(r => r.ProfileId == similarProfileId)
                         .Select(r => r.TitleId)
                         .ToListAsync(cancellationToken))
            {
                exclude.Add(id);
            }

            foreach (var id in await db.Watched.AsNoTracking()
                         .Where(w => w.ProfileId == similarProfileId)
                         .Select(w => w.TitleId)
                         .ToListAsync(cancellationToken))
            {
                exclude.Add(id);
            }
        }

        exclude.Add(query.TitleId);

        var excludeInLibrary = await ExcludesLibraryAsync(query.ProfileId, cancellationToken);
        if (excludeInLibrary)
        {
            var inLibrary = await db.ArrLibraryItems.AsNoTracking()
                .Where(a => a.TitleId != null)
                .Select(a => a.TitleId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
            foreach (var id in inLibrary)
            {
                exclude.Add(id);
            }
        }

        IReadOnlyList<(int TitleId, double Similarity)> similar;
        var state = await RecoProfileStates.FindAdminNoTrackingAsync(db, cancellationToken);
        var modelId = state?.ModelId ?? RecoModelCatalog.DefaultModelId;

        // Prefer embeddings without loading sparse features for the whole pool (SP-R3).
        var queryEmbRow = await db.TitleEmbeddings.AsNoTracking()
            .Where(e => e.ModelId == modelId && e.TitleId == query.TitleId)
            .Select(e => new { e.Vector })
            .FirstOrDefaultAsync(cancellationToken);

        if (queryEmbRow is not null)
        {
            var candidateIds = await NarrowSimilarCandidatesAsync(
                query.TitleId,
                enabledPoolSet,
                exclude,
                cancellationToken);
            var embRows = await db.TitleEmbeddings.AsNoTracking()
                .Where(e => e.ModelId == modelId && candidateIds.Contains(e.TitleId))
                .Select(e => new { e.TitleId, e.Vector })
                .ToListAsync(cancellationToken);
            var embMap = embRows.ToDictionary(
                e => e.TitleId,
                e => RecoEmbeddingService.BytesToFloats(e.Vector));
            similar = RecoKnn.TopSimilar(
                RecoEmbeddingService.BytesToFloats(queryEmbRow.Vector),
                embMap,
                limit,
                minSimilarity: 0.01,
                excludeTitleIds: exclude);
        }
        else
        {
            similar = await QuerySimilarSparseAsync(
                query.TitleId,
                enabledPoolSet,
                exclude,
                limit,
                cancellationToken);
        }

        if (similar.Count == 0)
        {
            return new SimilarTitlesDto([]);
        }

        var titleIds = similar.Select(s => s.TitleId).ToList();
        var languageSourceId = query.SourceId
            ?? enabledSourceIds.OrderBy(id => id).FirstOrDefault();
        CatalogSource? languageSource = null;
        if (languageSourceId > 0)
        {
            languageSource = await db.CatalogSources.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == languageSourceId, cancellationToken);
        }

        var language = languageSource?.MetadataLanguage ?? LanguageFallback.English;
        var newWindow = await GetIntSettingAsync(SettingsKeys.CatalogNewWindowDays, 14, cancellationToken);
        var newCutoff = clock.UtcNow.AddDays(-newWindow);

        var titles = await db.Titles.AsNoTracking()
            .Include(t => t.Localizations)
            .Include(t => t.TitleGenres).ThenInclude(tg => tg.Genre)!.ThenInclude(g => g!.Localizations)
            .Include(t => t.Favorites)
            .Include(t => t.WatchedItems)
            .Include(t => t.Ratings)
            .Include(t => t.Availabilities).ThenInclude(a => a.CatalogSource)!.ThenInclude(s => s!.Provider)
            .Where(t => titleIds.Contains(t.Id))
            .ToListAsync(cancellationToken);
        var titlesById = titles.ToDictionary(t => t.Id);

        var arrItems = await db.ArrLibraryItems.AsNoTracking()
            .Where(a => a.TitleId != null && titleIds.Contains(a.TitleId.Value))
            .ToListAsync(cancellationToken);
        var arrByTitle = arrItems
            .Where(a => a.TitleId is not null)
            .GroupBy(a => a.TitleId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var items = new List<RecommendationItemDto>(similar.Count);
        foreach (var (titleId, similarity) in similar)
        {
            if (!titlesById.TryGetValue(titleId, out var title))
            {
                continue;
            }

            arrByTitle.TryGetValue(titleId, out var arrItem);
            var fakeRec = new Recommendation
            {
                Version = 0,
                TitleId = titleId,
                Score = similarity,
                Personal = null,
                Prior = 0,
                Rank = 0,
                ReasonsJson = "[]",
            };
            var mapped = MapItem(
                title,
                fakeRec,
                RecoMembership.ScopeEnabled,
                query.SourceId,
                enabledSourceIds,
                language,
                newCutoff,
                arrItem,
                query.ProfileId);
            items.Add(mapped with
            {
                Reco = mapped.Reco with { Similarity = Math.Round(similarity, 4) },
            });
        }

        return new SimilarTitlesDto(items);
    }

    private static IReadOnlyList<RecoReasonDto> ParseReasons(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var list = new List<RecoReasonDto>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var kind = el.TryGetProperty("kind", out var k) ? k.GetString() : null;
                if (string.IsNullOrWhiteSpace(kind))
                {
                    continue;
                }

                int? titleId = el.TryGetProperty("titleId", out var tid) && tid.ValueKind == JsonValueKind.Number
                    ? tid.GetInt32()
                    : null;
                var name = el.TryGetProperty("name", out var n) ? n.GetString() : null;
                double? similarity = el.TryGetProperty("similarity", out var s) && s.ValueKind == JsonValueKind.Number
                    ? s.GetDouble()
                    : null;
                IReadOnlyList<string>? terms = null;
                if (el.TryGetProperty("terms", out var t) && t.ValueKind == JsonValueKind.Array)
                {
                    terms = t.EnumerateArray()
                        .Select(x => x.GetString())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Cast<string>()
                        .ToList();
                }

                list.Add(new RecoReasonDto(kind, titleId, name, similarity, terms));
            }

            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<HashSet<int>> LoadEnabledSourceIdsAsync(CancellationToken cancellationToken)
    {
        var ids = await db.CatalogSources.AsNoTracking()
            .Where(s => s.IsEnabled && s.Provider != null && s.Provider.IsEnabled)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    /// <summary>
    /// Library exclusion follows the profile. A missing profile keeps the column default.
    /// The global <c>reco.excludeInLibrary</c> key is not read.
    /// </summary>
    private async Task<bool> ExcludesLibraryAsync(int? profileId, CancellationToken cancellationToken)
    {
        if (profileId is not int id)
        {
            return true;
        }

        var exclude = await db.Profiles.AsNoTracking()
            .Where(profile => profile.Id == id)
            .Select(profile => (bool?)profile.ExcludeInLibrary)
            .SingleOrDefaultAsync(cancellationToken);
        return exclude ?? true;
    }

    private async Task EnsureEnabledAsync(CancellationToken cancellationToken)
    {
        if (!await GetBoolSettingAsync(SettingsKeys.RecoEnabled, true, cancellationToken))
        {
            throw new RecoDisabledException();
        }
    }

    private async Task<bool> GetBoolSettingAsync(string key, bool fallback, CancellationToken ct)
    {
        var raw = await settings.GetRawAsync(key, ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<bool>(raw);
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private async Task<int> GetIntSettingAsync(string key, int fallback, CancellationToken ct)
    {
        var raw = await settings.GetRawAsync(key, ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<int>(raw);
        }
        catch (JsonException)
        {
            return int.TryParse(raw.Trim('"'), out var n) ? n : fallback;
        }
    }

    private async Task<double> GetDoubleSettingAsync(string key, double fallback, CancellationToken ct)
    {
        var raw = await settings.GetRawAsync(key, ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<double>(raw);
        }
        catch (JsonException)
        {
            return double.TryParse(raw.Trim('"'), out var n) ? n : fallback;
        }
    }

    private static (string Name, string? Overview) ResolveLocalization(Title title, string preferredLang)
    {
        var preferred = title.Localizations.FirstOrDefault(l =>
            string.Equals(l.Language, preferredLang, StringComparison.OrdinalIgnoreCase));
        if (preferred is not null)
        {
            return (preferred.Name, preferred.Overview);
        }

        var english = title.Localizations.FirstOrDefault(l =>
            string.Equals(l.Language, LanguageFallback.English, StringComparison.OrdinalIgnoreCase));
        if (english is not null)
        {
            return (english.Name, english.Overview);
        }

        var any = title.Localizations.FirstOrDefault();
        return any is not null
            ? (any.Name, any.Overview)
            : (title.OriginalTitle ?? $"Title {title.Id}", null);
    }

    private static IReadOnlyList<GenreRefDto> MapGenres(Title title, string preferredLang) =>
        title.TitleGenres
            .Select(tg =>
            {
                var name = tg.Genre?.Localizations
                               .Where(l => l.Language == preferredLang || l.Language == LanguageFallback.English)
                               .OrderByDescending(l => l.Language == preferredLang)
                               .Select(l => l.Name)
                               .FirstOrDefault()
                           ?? $"Genre {tg.GenreId}";
                return new GenreRefDto(tg.GenreId, name);
            })
            .ToList();

    private static ArrSummaryDto BuildArrSummary(string mediaType, ArrLibraryItem? arrItem)
    {
        var kind = mediaType == MediaTypes.Series ? "sonarr" : "radarr";
        if (arrItem is null)
        {
            return new ArrSummaryDto(kind, "notInLibrary");
        }

        return new ArrSummaryDto(
            arrItem.Kind.ToLowerInvariant(),
            ArrKindExtensions.ParseState(arrItem.State).ToApi(),
            arrItem.Monitored,
            arrItem.QueueProgress);
    }

    private static string? MapTypeToCategory(string type) => type.Trim().ToLowerInvariant() switch
    {
        "movie" => TitleCategories.Movie,
        "series" => TitleCategories.Series,
        "standup" or "stand-up" or "stand_up" => TitleCategories.StandUp,
        _ => null,
    };

    private static string ToApiCategory(string category) => category switch
    {
        TitleCategories.Movie => "movie",
        TitleCategories.Series => "series",
        TitleCategories.StandUp => "standup",
        _ => category.ToLowerInvariant(),
    };
}
