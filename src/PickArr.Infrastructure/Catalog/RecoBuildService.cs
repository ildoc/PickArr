using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Metadata;
using PickArr.Core.Recommendations;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Metadata.Reco;
using PickArr.Infrastructure.Persistence;

namespace PickArr.Infrastructure.Catalog;

public sealed class RecoBuildService(
    PickArrDbContext db,
    IClock clock,
    ISettingsStore settings,
    IRecoLevel3Model level3Model) : IRecoBuildService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<int> BuildAsync(CancellationToken cancellationToken = default)
    {
        var sources = await db.CatalogSources
            .AsNoTracking()
            .Include(s => s.Provider)
            .Select(s => new RecoSourceFact(
                s.Id,
                s.IsEnabled,
                s.Provider != null && s.Provider.IsEnabled))
            .ToListAsync(cancellationToken);

        var enabledSourceIds = RecoMembership.EnabledSourceIds(sources);
        if (enabledSourceIds.Count == 0)
        {
            return await PublishEmptyAsync(cancellationToken);
        }

        // Pool: any Availability on enabled source+provider; RemovedAt is irrelevant (BR-REC-07).
        var poolTitleIds = await db.Availabilities
            .AsNoTracking()
            .Where(a => enabledSourceIds.Contains(a.CatalogSourceId))
            .Select(a => a.TitleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (poolTitleIds.Count == 0)
        {
            return await PublishEmptyAsync(cancellationToken);
        }

        var poolIdSet = poolTitleIds.ToHashSet();

        var titles = await db.Titles
            .AsNoTracking()
            .Where(t => poolIdSet.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                t.VoteTmdb,
                t.VoteTmdbCount,
                t.Popularity,
                t.Category,
                t.OriginalLanguage,
                t.ReleaseYear,
                t.OriginCountries,
                t.OriginalTitle,
            })
            .ToListAsync(cancellationToken);

        var genreRows = await db.TitleGenres.AsNoTracking()
            .Where(tg => poolIdSet.Contains(tg.TitleId))
            .Select(tg => new { tg.TitleId, tg.GenreId })
            .ToListAsync(cancellationToken);
        var genresByTitle = genreRows
            .GroupBy(g => g.TitleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(x => x.GenreId).ToList());

        var keywordRows = await db.TitleKeywords.AsNoTracking()
            .Where(tk => poolIdSet.Contains(tk.TitleId))
            .Select(tk => new { tk.TitleId, tk.KeywordId })
            .ToListAsync(cancellationToken);
        var keywordsByTitle = keywordRows
            .GroupBy(k => k.TitleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(x => x.KeywordId).ToList());

        var creditRows = await db.TitleCredits.AsNoTracking()
            .Where(tc => poolIdSet.Contains(tc.TitleId))
            .Select(tc => new { tc.TitleId, tc.PersonId, tc.Role })
            .ToListAsync(cancellationToken);
        var castByTitle = creditRows
            .Where(c => c.Role == CreditRoles.Cast)
            .GroupBy(c => c.TitleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(x => x.PersonId).Distinct().ToList());
        var directorByTitle = creditRows
            .Where(c => c.Role == CreditRoles.Director || c.Role == CreditRoles.Creator)
            .GroupBy(c => c.TitleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(x => x.PersonId).Distinct().ToList());

        var sparseFeatures = titles.Select(t => new RecoSparseTitleFeatures(
            t.Id,
            t.Category,
            t.OriginalLanguage,
            t.ReleaseYear,
            genresByTitle.GetValueOrDefault(t.Id) ?? [],
            keywordsByTitle.GetValueOrDefault(t.Id) ?? [],
            castByTitle.GetValueOrDefault(t.Id) ?? [],
            directorByTitle.GetValueOrDefault(t.Id) ?? [],
            ParseCountries(t.OriginCountries))).ToList();

        var titleVectors = RecoSparse.BuildTitleVectors(sparseFeatures);

        var useImplicit = await GetBoolSettingAsync(SettingsKeys.RecoUseImplicit, true, cancellationToken);
        var weights = new RecoSignalWeights(
            await GetDoubleSettingAsync(SettingsKeys.RecoWeightLove, RecoSignals.DefaultWeightLove, cancellationToken),
            await GetDoubleSettingAsync(SettingsKeys.RecoWeightLike, RecoSignals.DefaultWeightLike, cancellationToken),
            await GetDoubleSettingAsync(SettingsKeys.RecoWeightDislike, RecoSignals.DefaultWeightDislike, cancellationToken),
            await GetDoubleSettingAsync(
                SettingsKeys.RecoWeightNotInterested,
                RecoSignals.DefaultWeightNotInterested,
                cancellationToken),
            await GetDoubleSettingAsync(SettingsKeys.RecoWeightFavorite, RecoSignals.DefaultWeightFavorite, cancellationToken),
            await GetDoubleSettingAsync(SettingsKeys.RecoWeightArrAdd, RecoSignals.DefaultWeightArrAdd, cancellationToken),
            await GetDoubleSettingAsync(SettingsKeys.RecoWeightDownloaded, RecoSignals.DefaultWeightDownloaded, cancellationToken),
            await GetDoubleSettingAsync(SettingsKeys.RecoWeightWatched, RecoSignals.DefaultWeightWatched, cancellationToken));
        var n0 = await GetDoubleSettingAsync(SettingsKeys.RecoN0, RecoLearning.DefaultN0, cancellationToken);

        // Signals are global (all titles), not limited to the current pool.
        var signalFacts = await LoadSignalFactsAsync(cancellationToken);
        var signalResult = RecoSignals.Compute(signalFacts, weights, useImplicit);
        var nEff = signalResult.EffectiveN;
        var stage = RecoLearning.StageFromEffectiveN(nEff, n0);
        var personalWeight = RecoLearning.PersonalWeight(nEff, n0);

        var profile = RecoSparse.BuildProfile(signalResult.Signals, titleVectors);
        var sparseRaw = RecoSparse.ScoreAll(profile, titleVectors);
        var sparseNorm = RecoNormalize.MinMaxByTitle(sparseRaw);

        var minCoverage = await GetDoubleSettingAsync(SettingsKeys.RecoEmbeddingsMinCoverage, 0.8, cancellationToken);
        var embeddingsEnabled = await GetBoolSettingAsync(SettingsKeys.RecoEmbeddingsEnabled, true, cancellationToken);
        var statePeek = await db.RecoStates.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == RecoState.SingletonId, cancellationToken);
        var coverage = statePeek?.EmbeddingCoverage ?? 0;
        var modelReady = string.Equals(statePeek?.ModelStatus, RecoModelStatuses.Ready, StringComparison.OrdinalIgnoreCase);
        var useL2 = embeddingsEnabled && modelReady && coverage >= minCoverage && nEff > 0;

        IReadOnlyDictionary<int, double> knnUnit = new Dictionary<int, double>();
        if (useL2)
        {
            var modelId = statePeek?.ModelId ?? RecoModelCatalog.DefaultModelId;
            var embRows = await db.TitleEmbeddings.AsNoTracking()
                .Where(e => e.ModelId == modelId && poolIdSet.Contains(e.TitleId))
                .Select(e => new { e.TitleId, e.Vector })
                .ToListAsync(cancellationToken);
            var embMap = embRows.ToDictionary(
                e => e.TitleId,
                e => RecoEmbeddingService.BytesToFloats(e.Vector));
            if (embMap.Count > 0)
            {
                var knnRaw = RecoKnn.ScoreAll(embMap, signalResult.Signals);
                var knn01 = knnRaw.ToDictionary(kv => kv.Key, kv => RecoKnn.ToUnitInterval(kv.Value));
                knnUnit = RecoNormalize.MinMaxByTitle(knn01);
            }
            else
            {
                useL2 = false;
            }
        }

        // a=0.4, b=0.6 when L2; a=1 when L1 only (§12.1).
        var weightSparse = useL2 ? 0.4 : 1.0;
        var weightKnn = useL2 ? 0.6 : 0.0;

        var priorStats = titles
            .Select(t => new RecoTitleStats(t.Id, t.VoteTmdb, t.VoteTmdbCount, t.Popularity))
            .ToList();
        var priors = RecoPrior.Compute(priorStats);
        var priorById = priors.ToDictionary(p => p.TitleId);

        var minRatings = await GetIntSettingAsync(
            SettingsKeys.RecoMlMinRatings,
            RecoLevel3.DefaultMinRatings,
            cancellationToken);
        var level3Active = statePeek?.Level3Active == true;
        IRecoLevel3Scorer? level3Scorer = null;
        if (level3Active && nEff > 0)
        {
            var ratingRows = await db.Ratings.AsNoTracking()
                .Select(r => new { r.TitleId, r.Value })
                .ToListAsync(cancellationToken);
            var examples = new List<RecoLevel3Example>();
            var titleById = titles.ToDictionary(t => t.Id);
            foreach (var row in ratingRows)
            {
                if (!titleById.TryGetValue(row.TitleId, out var t))
                {
                    continue;
                }

                var sparse = sparseNorm.GetValueOrDefault(row.TitleId, 0.5);
                var knn = knnUnit.GetValueOrDefault(row.TitleId, 0.5);
                var priorT = priorById.TryGetValue(row.TitleId, out var ps) ? ps.Prior : 0.5;
                examples.Add(new RecoLevel3Example(
                    row.TitleId,
                    RatingValues.IsPositive(row.Value),
                    RecoLevel3.BuildFeatures(
                        titleVectors.GetValueOrDefault(row.TitleId),
                        sparse,
                        knn,
                        priorT,
                        t.ReleaseYear,
                        t.VoteTmdb,
                        t.Popularity)));
            }

            level3Scorer = level3Model.TryTrain(examples, minRatings, RecoEvalMetrics.Level3MinPerClass);
        }

        var titleNames = await LoadTitleNamesAsync(poolIdSet, cancellationToken);
        var termLabels = await LoadTermLabelsAsync(profile, cancellationToken);
        var positiveCandidates = RecoReasons.BuildPositiveCandidates(signalResult.Signals, titleVectors);

        string? ResolveTerm(string key) =>
            termLabels.TryGetValue(key, out var label) ? label : null;

        var scored = new List<(int TitleId, double Score, double? Personal, double Prior, string ReasonsJson)>(titles.Count);
        foreach (var title in titles)
        {
            var prior = priorById[title.Id].Prior;
            double? personal = null;
            double score;
            if (nEff <= 0 || personalWeight <= 0 || profile.IsEmpty)
            {
                score = prior;
            }
            else
            {
                var sparse = sparseNorm.GetValueOrDefault(title.Id, 0.5);
                var knn = knnUnit.GetValueOrDefault(title.Id, 0.5);
                personal = weightSparse * sparse + weightKnn * knn;
                if (level3Scorer is not null)
                {
                    personal = level3Scorer.Score(RecoLevel3.BuildFeatures(
                        titleVectors.GetValueOrDefault(title.Id),
                        sparse,
                        knn,
                        prior,
                        title.ReleaseYear,
                        title.VoteTmdb,
                        title.Popularity));
                }

                score = RecoLearning.FuseScore(prior, personal.Value, personalWeight);
            }

            var reasons = RecoReasons.Build(
                title.Id,
                priorNorm: prior,
                personalNorm: personal ?? 0,
                personalWeight: personalWeight,
                profile: profile.IsEmpty ? null : profile,
                titleVector: titleVectors.GetValueOrDefault(title.Id),
                titleVectors: titleVectors,
                signals: signalResult.Signals,
                titleNames: titleNames,
                resolveTermLabel: ResolveTerm,
                positiveCandidates: positiveCandidates);

            // Cold start / no personal: keep popular-only.
            var reasonsJson = nEff <= 0 || personalWeight <= 0
                ? RecoReasons.PopularOnlyJson
                : RecoReasons.Serialize(reasons);

            scored.Add((title.Id, score, personal, prior, reasonsJson));
        }

        var ordered = scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.TitleId)
            .ToList();

        var ratingCount = await db.Ratings.CountAsync(cancellationToken);
        var now = clock.UtcNow;
        var profileJson = profile.IsEmpty
            ? null
            : JsonSerializer.Serialize(
                profile.Weights
                    .OrderByDescending(kv => Math.Abs(kv.Value))
                    .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                    .ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value, 6)),
                JsonOptions);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var state = await db.RecoStates.FirstOrDefaultAsync(s => s.Id == RecoState.SingletonId, cancellationToken);
        if (state is null)
        {
            state = new RecoState { Id = RecoState.SingletonId };
            db.RecoStates.Add(state);
        }

        var newVersion = state.CurrentVersion + 1;
        var oldVersion = state.CurrentVersion;

        var rows = new List<Recommendation>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var s = ordered[i];
            rows.Add(new Recommendation
            {
                Version = newVersion,
                TitleId = s.TitleId,
                Score = s.Score,
                Personal = s.Personal,
                Prior = s.Prior,
                Rank = i + 1,
                ReasonsJson = s.ReasonsJson,
            });
        }

        db.Recommendations.AddRange(rows);

        if (oldVersion > 0)
        {
            await db.Recommendations
                .Where(r => r.Version == oldVersion)
                .ExecuteDeleteAsync(cancellationToken);
        }

        state.CurrentVersion = newVersion;
        state.BuiltAt = now;
        state.RatingCount = ratingCount;
        state.EffectiveN = nEff;
        state.Stage = stage;
        state.ProfileJson = profileJson;
        state.LastError = null;

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return newVersion;
    }

    private async Task<IReadOnlyList<RecoSignalTitleFacts>> LoadSignalFactsAsync(CancellationToken cancellationToken)
    {
        var ratings = await db.Ratings.AsNoTracking()
            .Select(r => new { r.TitleId, r.Value })
            .ToListAsync(cancellationToken);
        var favorites = await db.Favorites.AsNoTracking()
            .Select(f => f.TitleId)
            .ToListAsync(cancellationToken);
        var favoriteSet = favorites.ToHashSet();

        var watched = await db.Watched.AsNoTracking()
            .Select(w => w.TitleId)
            .ToListAsync(cancellationToken);
        var watchedSet = watched.ToHashSet();

        var arrAdds = await db.ArrAuditLogs.AsNoTracking()
            .Where(a => a.TitleId != null
                        && a.Action == ArrAuditActions.Add
                        && a.Outcome == ArrAuditOutcomes.Success)
            .Select(a => a.TitleId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var arrAddSet = arrAdds.ToHashSet();

        var downloaded = await db.ArrLibraryItems.AsNoTracking()
            .Where(a => a.TitleId != null && a.State == ArrStates.Downloaded)
            .Select(a => a.TitleId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var downloadedSet = downloaded.ToHashSet();

        var titleIds = ratings.Select(r => r.TitleId)
            .Concat(favoriteSet)
            .Concat(watchedSet)
            .Concat(arrAddSet)
            .Concat(downloadedSet)
            .Distinct()
            .ToList();

        var ratingByTitle = ratings.ToDictionary(r => r.TitleId, r => r.Value);
        return titleIds.Select(id => new RecoSignalTitleFacts(
            id,
            ratingByTitle.GetValueOrDefault(id),
            favoriteSet.Contains(id),
            arrAddSet.Contains(id),
            downloadedSet.Contains(id),
            watchedSet.Contains(id))).ToList();
    }

    private async Task<IReadOnlyDictionary<int, string>> LoadTitleNamesAsync(
        HashSet<int> poolIds,
        CancellationToken cancellationToken)
    {
        // Prefer en-US localization for reason chips; fall back to original title.
        var rows = await db.Titles.AsNoTracking()
            .Where(t => poolIds.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                t.OriginalTitle,
                EnName = t.Localizations
                    .Where(l => l.Language == LanguageFallback.English)
                    .Select(l => l.Name)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            r => r.Id,
            r => r.EnName ?? r.OriginalTitle ?? $"Title {r.Id}");
    }

    private async Task<Dictionary<string, string>> LoadTermLabelsAsync(
        SparseVector profile,
        CancellationToken cancellationToken)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        if (profile.IsEmpty)
        {
            return labels;
        }

        var genreIds = new List<int>();
        var keywordIds = new List<int>();
        var personIds = new List<int>();

        foreach (var term in profile.Weights.Keys)
        {
            if (term.StartsWith(RecoSparse.PrefixGenre, StringComparison.Ordinal)
                && int.TryParse(term[RecoSparse.PrefixGenre.Length..], out var gid))
            {
                genreIds.Add(gid);
            }
            else if (term.StartsWith(RecoSparse.PrefixKeyword, StringComparison.Ordinal)
                     && int.TryParse(term[RecoSparse.PrefixKeyword.Length..], out var kid))
            {
                keywordIds.Add(kid);
            }
            else if ((term.StartsWith(RecoSparse.PrefixPerson, StringComparison.Ordinal)
                      || term.StartsWith(RecoSparse.PrefixDirector, StringComparison.Ordinal))
                     && int.TryParse(term[2..], out var pid))
            {
                personIds.Add(pid);
            }
            else if (term.StartsWith(RecoSparse.PrefixLanguage, StringComparison.Ordinal))
            {
                labels[term] = term[RecoSparse.PrefixLanguage.Length..];
            }
            else if (term.StartsWith(RecoSparse.PrefixCountry, StringComparison.Ordinal))
            {
                labels[term] = term[RecoSparse.PrefixCountry.Length..];
            }
            else if (term.StartsWith(RecoSparse.PrefixDecade, StringComparison.Ordinal))
            {
                labels[term] = term[RecoSparse.PrefixDecade.Length..] + "s";
            }
            else if (term.StartsWith(RecoSparse.PrefixType, StringComparison.Ordinal))
            {
                labels[term] = term[RecoSparse.PrefixType.Length..];
            }
        }

        if (genreIds.Count > 0)
        {
            var genres = await db.Genres.AsNoTracking()
                .Where(g => genreIds.Contains(g.Id))
                .Select(g => new
                {
                    g.Id,
                    Name = g.Localizations
                        .Where(l => l.Language == LanguageFallback.English)
                        .Select(l => l.Name)
                        .FirstOrDefault()
                        ?? g.Localizations.Select(l => l.Name).FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);
            foreach (var g in genres)
            {
                if (!string.IsNullOrWhiteSpace(g.Name))
                {
                    labels[RecoSparse.GenreTerm(g.Id)] = g.Name;
                }
            }
        }

        if (keywordIds.Count > 0)
        {
            var keywords = await db.Keywords.AsNoTracking()
                .Where(k => keywordIds.Contains(k.Id))
                .Select(k => new { k.Id, k.Name })
                .ToListAsync(cancellationToken);
            foreach (var k in keywords)
            {
                labels[RecoSparse.KeywordTerm(k.Id)] = k.Name;
            }
        }

        if (personIds.Count > 0)
        {
            var people = await db.People.AsNoTracking()
                .Where(p => personIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToListAsync(cancellationToken);
            foreach (var p in people)
            {
                labels[RecoSparse.CastTerm(p.Id)] = p.Name;
                labels[RecoSparse.DirectorTerm(p.Id)] = p.Name;
            }
        }

        return labels;
    }

    private static IReadOnlyList<string> ParseCountries(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<int> PublishEmptyAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var state = await db.RecoStates.FirstOrDefaultAsync(s => s.Id == RecoState.SingletonId, cancellationToken);
        if (state is null)
        {
            state = new RecoState { Id = RecoState.SingletonId };
            db.RecoStates.Add(state);
        }

        var newVersion = state.CurrentVersion + 1;
        var oldVersion = state.CurrentVersion;

        if (oldVersion > 0)
        {
            await db.Recommendations
                .Where(r => r.Version == oldVersion)
                .ExecuteDeleteAsync(cancellationToken);
        }

        state.CurrentVersion = newVersion;
        state.BuiltAt = now;
        state.RatingCount = await db.Ratings.CountAsync(cancellationToken);
        state.EffectiveN = 0;
        state.Stage = RecoLearning.StageCold;
        state.ProfileJson = null;
        state.LastError = null;

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return newVersion;
    }

    private async Task<int> GetIntSettingAsync(string key, int fallback, CancellationToken ct)
    {
        var raw = await settings.GetRawAsync(key, ct);
        return int.TryParse(raw?.Trim().Trim('"'), out var v) ? v : fallback;
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
            return double.TryParse(raw.Trim('"'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var n)
                ? n
                : fallback;
        }
    }
}
