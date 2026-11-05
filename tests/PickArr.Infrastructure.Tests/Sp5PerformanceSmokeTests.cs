using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Catalog;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Metadata;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Catalog;
using PickArr.Infrastructure.Persistence;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

/// <summary>
/// SP-5: seed ~10k titles and measure list/search latency (NFR-01 p95 &lt; 300 ms).
/// </summary>
public sealed class Sp5PerformanceSmokeTests
{
    private const int TitleCount = 10_000;
    private const int Iterations = 25;
    private const int Warmup = 5;

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
    public async Task List_and_search_p95_under_nfr01_with_10k_titles()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "PickArr-sp5", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        await using var connection = new SqliteConnection($"Data Source={dbPath};Cache=Shared");
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
        await cmd.ExecuteNonQueryAsync();

        var options = new DbContextOptionsBuilder<PickArrDbContext>().UseSqlite(connection).Options;
        await using var db = new PickArrDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var sourceId = await SeedAsync(db);
        var settings = new MemorySettingsStore();
        await settings.SetAsync(SettingsKeys.SonarrUrl, JsonSerializer.Serialize("http://sonarr"), false);
        await settings.SetAsync(SettingsKeys.SonarrApiKey, JsonSerializer.Serialize("key"), true);
        await settings.SetAsync(SettingsKeys.RadarrUrl, JsonSerializer.Serialize("http://radarr"), false);
        await settings.SetAsync(SettingsKeys.RadarrApiKey, JsonSerializer.Serialize("key"), true);

        var sut = new TitleQueryService(db, settings, new FixedClock(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc)));

        // Warm-up
        for (var i = 0; i < Warmup; i++)
        {
            await sut.QueryTitlesAsync(sourceId, new TitleQueryRequest(Page: 1, PageSize: 48, Sort: "added"));
            await sut.QueryTitlesAsync(sourceId, new TitleQueryRequest(Q: "title 42", Page: 1, PageSize: 48, Sort: "relevance"));
        }

        var listMs = await MeasureAsync(
            Iterations,
            () => sut.QueryTitlesAsync(sourceId, new TitleQueryRequest(Page: 1, PageSize: 48, Sort: "added")));
        var searchMs = await MeasureAsync(
            Iterations,
            () => sut.QueryTitlesAsync(sourceId, new TitleQueryRequest(Q: "title 4242", Page: 1, PageSize: 48, Sort: "relevance")));

        var listP95 = Percentile(listMs, 0.95);
        var searchP95 = Percentile(searchMs, 0.95);

        // Emit for SP-5 artifact capture
        Console.WriteLine(
            $"SP-5 results titles={TitleCount} list_p95_ms={listP95:F1} search_p95_ms={searchP95:F1} " +
            $"list_median_ms={Percentile(listMs, 0.5):F1} search_median_ms={Percentile(searchMs, 0.5):F1} " +
            $"db={dbPath} wal=on");

        listP95.ShouldBeLessThan(300, $"list p95 {listP95:F1} ms exceeds NFR-01 300 ms");
        searchP95.ShouldBeLessThan(300, $"search p95 {searchP95:F1} ms exceeds NFR-01 300 ms — consider FTS5");

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

    private static async Task<int> SeedAsync(PickArrDbContext db)
    {
        var now = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var provider = new Provider
        {
            Key = "netflix",
            DisplayName = "Netflix",
            ProviderType = "watchmode",
            ExternalId = "203",
            IsEnabled = true,
            CreatedAt = now,
        };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();

        var source = new CatalogSource
        {
            ProviderId = provider.Id,
            CountryCode = "US",
            MetadataLanguage = "en-US",
            IsEnabled = true,
            BaselineCompletedAt = now,
            LastFullAt = now,
        };
        db.CatalogSources.Add(source);
        await db.SaveChangesAsync();

        const int batch = 500;
        for (var start = 0; start < TitleCount; start += batch)
        {
            var end = Math.Min(start + batch, TitleCount);
            for (var i = start; i < end; i++)
            {
                var isMovie = i % 2 == 0;
                var title = new Title
                {
                    WatchmodeId = 2_000_000 + i,
                    MediaType = isMovie ? MediaTypes.Movie : MediaTypes.Series,
                    Category = isMovie ? TitleCategories.Movie : TitleCategories.Series,
                    OriginalTitle = $"Synth Title {i}",
                    OriginalTitleNormalized = $"synth title {i}",
                    ReleaseYear = 1990 + (i % 35),
                    VoteTmdb = 5.0 + (i % 50) / 10.0,
                    MetadataStatus = MetadataStatuses.Ready,
                    CreatedAt = now.AddMinutes(i),
                    UpdatedAt = now.AddMinutes(i),
                };
                db.Titles.Add(title);
            }

            await db.SaveChangesAsync();

            var titles = await db.Titles
                .Where(t => t.WatchmodeId >= 2_000_000 + start && t.WatchmodeId < 2_000_000 + end)
                .Select(t => new { t.Id, t.WatchmodeId, t.MediaType })
                .ToListAsync();

            foreach (var t in titles)
            {
                var idx = t.WatchmodeId!.Value - 2_000_000;
                db.TitleLocalizations.Add(new TitleLocalization
                {
                    TitleId = t.Id,
                    Language = "en-US",
                    Name = $"Localized Title {idx}",
                    NameNormalized = $"localized title {idx}",
                    Overview = "synth",
                    FetchedAt = now,
                });
                db.Availabilities.Add(new Availability
                {
                    TitleId = t.Id,
                    CatalogSourceId = source.Id,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    AddedAt = now.AddMinutes(idx),
                    IsBaseline = idx < 100,
                });

                if (idx % 5 == 0)
                {
                    db.ArrLibraryItems.Add(new ArrLibraryItem
                    {
                        Kind = t.MediaType == MediaTypes.Movie ? "Radarr" : "Sonarr",
                        ArrId = 10_000 + idx,
                        TitleId = t.Id,
                        ArrTitle = $"Synth Title {idx}",
                        Year = 1990 + (idx % 35),
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

        (await db.Titles.CountAsync()).ShouldBe(TitleCount);
        return source.Id;
    }
}
