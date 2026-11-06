using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Metadata;
using PickArr.Core.Recommendations;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Catalog;
using PickArr.Infrastructure.Metadata.Reco;
using PickArr.Infrastructure.Persistence;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

/// <summary>
/// SP-R3: measure RecoBuild + GET /recommendations latency (NFR-REC-01/02).
/// Fixture pool (not live .data) so results are reproducible.
/// </summary>
public sealed class SpR3PerformanceSmokeTests
{
    private const int TitleCount = 5_000;
    private const int SignalCount = 500;
    private const int GenreCount = 20;
    private const int KeywordCount = 100;
    private const int PersonCount = 200;
    private const int GetIterations = 20;
    private const int GetWarmup = 3;

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class MemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, (string Json, bool Secret)> _values = new(StringComparer.Ordinal);

        public Task<string?> GetRawAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(key, out var v) ? v.Json : null);

        public Task SetAsync(string key, string valueJson, bool isSecret, CancellationToken cancellationToken = default)
        {
            _values[key] = (valueJson, isSecret);
            return Task.CompletedTask;
        }

        public Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.ContainsKey(key));

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Rebuild_and_get_recommendations_latency_against_nfr()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "PickArr-spr3", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        await using var connection = new SqliteConnection($"Data Source={dbPath};Cache=Shared");
        await connection.OpenAsync();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
            await cmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<PickArrDbContext>().UseSqlite(connection).Options;
        await using var db = new PickArrDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
        var (sourceId, sourceIdB) = await SeedAsync(db, now);

        var settings = new MemorySettingsStore();
        // L1-only rebuild is the NFR-REC-02 baseline (embeddings are a separate job).
        await settings.SetAsync(SettingsKeys.RecoEnabled, JsonSerializer.Serialize(true), false);
        await settings.SetAsync(SettingsKeys.RecoEmbeddingsEnabled, JsonSerializer.Serialize(false), false);
        await settings.SetAsync(SettingsKeys.RecoExcludeInLibrary, JsonSerializer.Serialize(true), false);
        await settings.SetAsync(SettingsKeys.RecoMmrLambda, JsonSerializer.Serialize(0.7), false);
        await settings.SetAsync(SettingsKeys.RecoExploreRate, JsonSerializer.Serialize(0.0), false); // isolate MMR

        var clock = new FixedClock(now);
        var build = new RecoBuildService(db, clock, settings, new RecoLevel3Model());
        var query = new RecoQueryService(db, settings, clock);

        // --- Rebuild L1 (primary NFR-REC-02) ---
        var rebuildSw = Stopwatch.StartNew();
        var scored = await build.BuildAsync();
        rebuildSw.Stop();
        var rebuildMs = rebuildSw.Elapsed.TotalMilliseconds;
        scored.ShouldBeGreaterThan(0);

        var version = await db.RecoStates.AsNoTracking()
            .Select(s => s.CurrentVersion)
            .FirstAsync();
        version.ShouldBeGreaterThan(0);
        var recoCount = await db.Recommendations.CountAsync(r => r.Version == version);

        // L2 rebuild is measured on a thinner embedding set (same pool, synthetic vectors).
        // Seed can dominate wall time; keep embeddings for the full pool to hit coverage gate.
        var embSeedSw = Stopwatch.StartNew();
        await SeedSyntheticEmbeddingsAsync(db, now);
        embSeedSw.Stop();
        await settings.SetAsync(SettingsKeys.RecoEmbeddingsEnabled, JsonSerializer.Serialize(true), false);
        var state = await db.RecoStates.FirstAsync(s => s.Id == RecoState.SingletonId);
        state.ModelStatus = RecoModelStatuses.Ready;
        state.ModelId = RecoModelCatalog.DefaultModelId;
        state.EmbeddingCoverage = 1.0;
        await db.SaveChangesAsync();

        var rebuildL2Sw = Stopwatch.StartNew();
        await build.BuildAsync();
        rebuildL2Sw.Stop();
        var rebuildL2Ms = rebuildL2Sw.Elapsed.TotalMilliseconds;

        // Disable explore; measure GET with and without MMR.
        await settings.SetAsync(SettingsKeys.RecoExploreRate, JsonSerializer.Serialize(0.0), false);

        var similarWarmTitleId = await db.Titles.AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => t.Id)
            .FirstAsync();
        for (var i = 0; i < GetWarmup; i++)
        {
            await query.QueryAsync(new RecommendationsQuery(
                Scope: RecoMembership.ScopeSource,
                SourceId: sourceId,
                Diversify: false,
                Page: 1,
                PageSize: 48));
            await query.QueryAsync(new RecommendationsQuery(
                Scope: RecoMembership.ScopeSource,
                SourceId: sourceId,
                Diversify: true,
                Page: 1,
                PageSize: 48));
            await query.QuerySimilarAsync(new SimilarTitlesQuery(similarWarmTitleId, sourceId, Limit: 12));
        }

        var getPlainMs = await MeasureAsync(
            GetIterations,
            () => query.QueryAsync(new RecommendationsQuery(
                Scope: RecoMembership.ScopeSource,
                SourceId: sourceId,
                Diversify: false,
                IncludeInLibrary: false,
                Page: 1,
                PageSize: 48)));

        var getMmrMs = await MeasureAsync(
            GetIterations,
            () => query.QueryAsync(new RecommendationsQuery(
                Scope: RecoMembership.ScopeSource,
                SourceId: sourceId,
                Diversify: true,
                IncludeInLibrary: false,
                Page: 1,
                PageSize: 48)));

        var getEnabledMs = await MeasureAsync(
            GetIterations,
            () => query.QueryAsync(new RecommendationsQuery(
                Scope: RecoMembership.ScopeEnabled,
                Diversify: false,
                IncludeInLibrary: false,
                Page: 1,
                PageSize: 48)));

        // Multi-source membership (both sources enabled).
        _ = sourceIdB;

        var similarTitleId = await db.Titles.AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => t.Id)
            .FirstAsync();
        var getSimilarMs = await MeasureAsync(
            GetIterations,
            () => query.QuerySimilarAsync(new SimilarTitlesQuery(similarTitleId, sourceId, Limit: 12)));

        var getPlainP95 = Percentile(getPlainMs, 0.95);
        var getMmrP95 = Percentile(getMmrMs, 0.95);
        var getEnabledP95 = Percentile(getEnabledMs, 0.95);
        var getSimilarP95 = Percentile(getSimilarMs, 0.95);

        Console.WriteLine(
            $"SP-R3 results pool={TitleCount} signals={SignalCount} reco_rows={recoCount} " +
            $"rebuild_l1_ms={rebuildMs:F0} rebuild_l2_ms={rebuildL2Ms:F0} emb_seed_ms={embSeedSw.ElapsedMilliseconds} " +
            $"get_plain_p95_ms={getPlainP95:F1} get_mmr_p95_ms={getMmrP95:F1} " +
            $"get_enabled_p95_ms={getEnabledP95:F1} get_similar_p95_ms={getSimilarP95:F1} " +
            $"get_plain_median_ms={Percentile(getPlainMs, 0.5):F1} get_mmr_median_ms={Percentile(getMmrMs, 0.5):F1} " +
            $"db={dbPath} wal=on");

        // Soft assertions: spike documents gaps; hard-fail only on catastrophic regressions.
        rebuildMs.ShouldBeLessThan(60_000, $"L1 rebuild {rebuildMs:F0} ms is catastrophically slow");
        getPlainP95.ShouldBeLessThan(5_000, $"GET plain p95 {getPlainP95:F1} ms is catastrophically slow");

        try
        {
            File.Delete(dbPath);
            File.Delete(dbPath + "-wal");
            File.Delete(dbPath + "-shm");
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static async Task<List<double>> MeasureAsync(int iterations, Func<Task> action)
    {
        var samples = new List<double>(iterations);
        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await action();
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        return samples;
    }

    private static double Percentile(IReadOnlyList<double> samples, double p)
    {
        var sorted = samples.OrderBy(x => x).ToArray();
        var idx = (int)Math.Ceiling(p * sorted.Length) - 1;
        idx = Math.Clamp(idx, 0, sorted.Length - 1);
        return sorted[idx];
    }

    private static async Task<(int SourceA, int SourceB)> SeedAsync(PickArrDbContext db, DateTime now)
    {
        var providerA = new Provider
        {
            Key = "netflix",
            DisplayName = "Netflix",
            ProviderType = "watchmode",
            ExternalId = "203",
            IsEnabled = true,
            CreatedAt = now,
        };
        var providerB = new Provider
        {
            Key = "disney",
            DisplayName = "Disney+",
            ProviderType = "watchmode",
            ExternalId = "372",
            IsEnabled = true,
            CreatedAt = now,
        };
        db.Providers.AddRange(providerA, providerB);
        await db.SaveChangesAsync();

        var sourceA = new CatalogSource
        {
            ProviderId = providerA.Id,
            CountryCode = "IT",
            MetadataLanguage = "en-US",
            IsEnabled = true,
            BaselineCompletedAt = now,
            LastFullAt = now,
        };
        var sourceB = new CatalogSource
        {
            ProviderId = providerB.Id,
            CountryCode = "IT",
            MetadataLanguage = "en-US",
            IsEnabled = true,
            BaselineCompletedAt = now,
            LastFullAt = now,
        };
        db.CatalogSources.AddRange(sourceA, sourceB);
        await db.SaveChangesAsync();

        for (var g = 1; g <= GenreCount; g++)
        {
            db.Genres.Add(new Genre { Id = g });
            db.GenreLocalizations.Add(new GenreLocalization
            {
                GenreId = g,
                Language = "en-US",
                Name = $"Genre {g}",
            });
        }

        for (var k = 1; k <= KeywordCount; k++)
        {
            db.Keywords.Add(new Keyword { Id = k, Name = $"kw{k}" });
        }

        for (var p = 1; p <= PersonCount; p++)
        {
            db.People.Add(new Person { Id = p, Name = $"Person {p}" });
        }

        await db.SaveChangesAsync();

        const int batch = 250;
        for (var start = 0; start < TitleCount; start += batch)
        {
            var end = Math.Min(start + batch, TitleCount);
            for (var i = start; i < end; i++)
            {
                var isMovie = i % 2 == 0;
                db.Titles.Add(new Title
                {
                    WatchmodeId = 3_000_000 + i,
                    MediaType = isMovie ? MediaTypes.Movie : MediaTypes.Series,
                    Category = isMovie ? TitleCategories.Movie : TitleCategories.Series,
                    OriginalTitle = $"Reco Synth {i}",
                    OriginalTitleNormalized = $"reco synth {i}",
                    OriginalLanguage = i % 3 == 0 ? "en" : "it",
                    ReleaseYear = 1980 + (i % 45),
                    VoteTmdb = 4.0 + (i % 60) / 10.0,
                    VoteTmdbCount = 50 + (i % 5000),
                    Popularity = 1.0 + (i % 200),
                    MetadataStatus = MetadataStatuses.Ready,
                    OriginCountries = i % 2 == 0 ? "[\"US\"]" : "[\"IT\"]",
                    CreatedAt = now.AddMinutes(i),
                    UpdatedAt = now.AddMinutes(i),
                });
            }

            await db.SaveChangesAsync();

            var titles = await db.Titles
                .Where(t => t.WatchmodeId >= 3_000_000 + start && t.WatchmodeId < 3_000_000 + end)
                .Select(t => new { t.Id, t.WatchmodeId, t.MediaType })
                .ToListAsync();

            foreach (var t in titles)
            {
                var idx = t.WatchmodeId!.Value - 3_000_000;
                db.TitleLocalizations.Add(new TitleLocalization
                {
                    TitleId = t.Id,
                    Language = "en-US",
                    Name = $"Reco Localized {idx}",
                    NameNormalized = $"reco localized {idx}",
                    Overview = "synth overview",
                    FetchedAt = now,
                });

                // Dual-source pool: ~70% on A, ~40% on B, overlap ~10%.
                if (idx % 10 != 7 && idx % 10 != 8 && idx % 10 != 9)
                {
                    db.Availabilities.Add(new Availability
                    {
                        TitleId = t.Id,
                        CatalogSourceId = sourceA.Id,
                        FirstSeenAt = now,
                        LastSeenAt = now,
                        AddedAt = now.AddMinutes(idx),
                    });
                }

                if (idx % 10 >= 6)
                {
                    db.Availabilities.Add(new Availability
                    {
                        TitleId = t.Id,
                        CatalogSourceId = sourceB.Id,
                        FirstSeenAt = now,
                        LastSeenAt = now,
                        AddedAt = now.AddMinutes(idx),
                    });
                }

                var g1 = (idx % GenreCount) + 1;
                var g2 = ((idx + 7) % GenreCount) + 1;
                db.TitleGenres.Add(new TitleGenre { TitleId = t.Id, GenreId = g1 });
                if (g2 != g1)
                {
                    db.TitleGenres.Add(new TitleGenre { TitleId = t.Id, GenreId = g2 });
                }

                var k1 = (idx % KeywordCount) + 1;
                var k2 = ((idx + 11) % KeywordCount) + 1;
                db.TitleKeywords.Add(new TitleKeyword { TitleId = t.Id, KeywordId = k1 });
                if (k2 != k1)
                {
                    db.TitleKeywords.Add(new TitleKeyword { TitleId = t.Id, KeywordId = k2 });
                }

                var cast1 = (idx % PersonCount) + 1;
                var director = ((idx + 41) % PersonCount) + 1;
                db.TitleCredits.Add(new TitleCredit
                {
                    TitleId = t.Id,
                    PersonId = cast1,
                    Role = CreditRoles.Cast,
                    Order = 0,
                });
                db.TitleCredits.Add(new TitleCredit
                {
                    TitleId = t.Id,
                    PersonId = director == cast1 ? ((director % PersonCount) + 1) : director,
                    Role = CreditRoles.Director,
                    Order = 0,
                });

                if (idx % 5 == 0)
                {
                    db.ArrLibraryItems.Add(new ArrLibraryItem
                    {
                        Kind = t.MediaType == MediaTypes.Movie ? "Radarr" : "Sonarr",
                        ArrId = 20_000 + idx,
                        TitleId = t.Id,
                        ArrTitle = $"Reco Synth {idx}",
                        Year = 1980 + (idx % 45),
                        HasFile = idx % 10 == 0,
                        Monitored = true,
                        State = idx % 10 == 0 ? "Downloaded" : "Missing",
                        SyncedAt = now,
                    });
                }
            }

            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        // 500 explicit ratings (signals).
        var rateIds = await db.Titles.AsNoTracking()
            .OrderBy(t => t.Id)
            .Take(SignalCount)
            .Select(t => t.Id)
            .ToListAsync();
        for (var i = 0; i < rateIds.Count; i++)
        {
            var value = i % 4 == 0 ? RatingValues.Dislike : RatingValues.Like;
            db.Ratings.Add(new Rating
            {
                TitleId = rateIds[i],
                Value = value,
                RatedAt = now.AddMinutes(-i),
            });
            db.RatingEvents.Add(new RatingEvent
            {
                TitleId = rateIds[i],
                Value = value,
                At = now.AddMinutes(-i),
                Origin = RatingEventOrigins.Ui,
            });
        }

        await db.SaveChangesAsync();
        (await db.Titles.CountAsync()).ShouldBe(TitleCount);
        (await db.Ratings.CountAsync()).ShouldBe(SignalCount);
        return (sourceA.Id, sourceB.Id);
    }

    private static async Task SeedSyntheticEmbeddingsAsync(PickArrDbContext db, DateTime now)
    {
        var ids = await db.Titles.AsNoTracking().Select(t => t.Id).ToListAsync();
        const int dim = RecoModelCatalog.EmbeddingDimensions;
        var rng = new Random(42);
        foreach (var chunk in ids.Chunk(200))
        {
            foreach (var id in chunk)
            {
                var vector = new float[dim];
                for (var d = 0; d < dim; d++)
                {
                    vector[d] = (float)(rng.NextDouble() * 2 - 1);
                }

                RecoKnn.L2Normalize(vector);
                db.TitleEmbeddings.Add(new TitleEmbedding
                {
                    TitleId = id,
                    ModelId = RecoModelCatalog.DefaultModelId,
                    Vector = RecoEmbeddingService.FloatsToBytes(vector),
                    TextHash = $"synth-{id}",
                    UpdatedAt = now,
                });
            }

            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }
}
