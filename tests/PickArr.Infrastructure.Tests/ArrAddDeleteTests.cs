using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Arr;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Arr;
using Pickarr.Infrastructure.Catalog;
using Pickarr.Infrastructure.Persistence;
using Pickarr.Infrastructure.Time;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Pickarr.Infrastructure.Tests;

public sealed class ArrAddDeleteTests : IAsyncLifetime
{
    private readonly WireMockServer _server = WireMockServer.Start();
    private readonly string _fixtureRoot = FindFixtureRoot();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _server.Stop();
        _server.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SonarrClient_AddAsync_posts_lookup_payload()
    {
        MapFixture("/api/v3/series/lookup", "sonarr/series-lookup.json");
        MapPostFixture("/api/v3/series", "sonarr/series-added.json");

        var settings = CreateSettings(sonarr: true);
        var http = new HttpClient { BaseAddress = new Uri(_server.Urls[0]) };
        var client = new SonarrClient(http, settings, NullLogger<SonarrClient>.Instance);

        var added = await client.AddAsync(new SonarrAddRequest(
            TvdbId: 121361,
            QualityProfileId: 1,
            RootFolderPath: "/tv",
            Monitored: true,
            Tags: [],
            Monitor: "all",
            MonitorNewItems: "all",
            SeriesType: "standard",
            SeasonFolder: true,
            SearchForMissingEpisodes: true));

        added.ArrId.ShouldBe(77);
        added.TvdbId.ShouldBe(121361);
    }

    [Fact]
    public async Task SonarrClient_AddAsync_looks_up_by_tmdb_when_tvdb_is_missing()
    {
        MapFixture("/api/v3/series/lookup", "sonarr/series-lookup.json");
        MapPostFixture("/api/v3/series", "sonarr/series-added.json");

        var settings = CreateSettings(sonarr: true);
        var http = new HttpClient { BaseAddress = new Uri(_server.Urls[0]) };
        var client = new SonarrClient(http, settings, NullLogger<SonarrClient>.Instance);

        var added = await client.AddAsync(new SonarrAddRequest(
            TvdbId: null,
            QualityProfileId: 1,
            RootFolderPath: "/tv",
            Monitored: true,
            Tags: [],
            Monitor: "all",
            MonitorNewItems: "all",
            SeriesType: "standard",
            SeasonFolder: true,
            SearchForMissingEpisodes: false,
            TmdbId: 1396));

        added.TvdbId.ShouldBe(121361);
        var lookup = _server.LogEntries.Single(entry =>
            entry.RequestMessage.Path.Contains("lookup", StringComparison.OrdinalIgnoreCase));
        lookup.RequestMessage.RawQuery.ShouldContain("tmdb");
    }

    [Fact]
    public async Task SonarrClient_AddAsync_monitors_only_the_selected_seasons()
    {
        MapFixture("/api/v3/series/lookup", "sonarr/series-lookup.json");
        MapPostFixture("/api/v3/series", "sonarr/series-added.json");

        var settings = CreateSettings(sonarr: true);
        var http = new HttpClient { BaseAddress = new Uri(_server.Urls[0]) };
        var client = new SonarrClient(http, settings, NullLogger<SonarrClient>.Instance);

        await client.AddAsync(new SonarrAddRequest(
            TvdbId: 121361,
            QualityProfileId: 1,
            RootFolderPath: "/tv",
            Monitored: true,
            Tags: [],
            Monitor: "all",
            MonitorNewItems: "none",
            SeriesType: "standard",
            SeasonFolder: true,
            SearchForMissingEpisodes: true,
            Seasons: [2]));

        var post = _server.LogEntries.Last(entry =>
            string.Equals(entry.RequestMessage.Method, "POST", StringComparison.OrdinalIgnoreCase));
        var body = JsonNode.Parse(post.RequestMessage.Body!)!;
        body["addOptions"]!["monitor"]!.GetValue<string>().ShouldBe("skip");
        body["monitored"]!.GetValue<bool>().ShouldBeTrue();
        var seasons = body["seasons"]!.AsArray();
        seasons.Single(season => season!["seasonNumber"]!.GetValue<int>() == 1)!["monitored"]!.GetValue<bool>().ShouldBeFalse();
        seasons.Single(season => season!["seasonNumber"]!.GetValue<int>() == 2)!["monitored"]!.GetValue<bool>().ShouldBeTrue();
        seasons.Single(season => season!["seasonNumber"]!.GetValue<int>() == 0)!["monitored"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public async Task ArrAddService_adds_series_and_writes_audit()
    {
        MapJson("/api/v3/series", "[]"); // find by tvdb
        MapFixture("/api/v3/series/lookup", "sonarr/series-lookup.json");
        MapPostFixture("/api/v3/series", "sonarr/series-added.json");

        await using var db = await CreateDbAsync();
        var title = await SeedSeriesAsync(db);
        var settings = CreateSettings(sonarr: true);
        var services = CreateServices(db, settings);

        var result = await services.Add.AddAsync(title.Id, new SonarrAddOptions(
            QualityProfileId: 1,
            RootFolderPath: "/tv",
            Monitor: "all",
            MonitorNewItems: "all",
            SeriesType: "standard",
            SeasonFolder: true,
            Tags: [],
            SearchNow: true));

        result.CanDelete.ShouldBeTrue();
        result.ArrId.ShouldBe(77);
        result.State.ShouldBe("missing");
        (await db.ArrLibraryItems.CountAsync()).ShouldBe(1);
        var audit = await db.ArrAuditLogs.SingleAsync();
        audit.Action.ShouldBe(ArrAuditActions.Add);
        audit.Outcome.ShouldBe(ArrAuditOutcomes.Success);
    }

    [Fact]
    public async Task ArrAddService_already_exists_returns_conflict_and_refreshes_cache()
    {
        MapFixture("/api/v3/series", "sonarr/series-library.json"); // find returns id 55

        await using var db = await CreateDbAsync();
        var title = await SeedSeriesAsync(db);
        var settings = CreateSettings(sonarr: true);
        var services = CreateServices(db, settings);

        var ex = await Should.ThrowAsync<ArrOperationException>(() =>
            services.Add.AddAsync(title.Id, new SonarrAddOptions(
                1, "/tv", "all", "all", "standard", true, [], false)));

        ex.Code.ShouldBe("ARR_ALREADY_EXISTS");
        (await db.ArrLibraryItems.CountAsync(a => a.ArrId == 55)).ShouldBe(1);
        (await db.ArrAuditLogs.CountAsync(a => a.Outcome == ArrAuditOutcomes.Failed)).ShouldBe(1);
    }

    [Fact]
    public async Task ArrDeleteService_removes_cache_and_keeps_title()
    {
        MapDeleteOk("/api/v3/series/55");

        await using var db = await CreateDbAsync();
        var title = await SeedSeriesAsync(db);
        db.ArrLibraryItems.Add(new ArrLibraryItem
        {
            Kind = ArrKinds.Sonarr,
            ArrId = 55,
            TitleId = title.Id,
            ArrTitle = "Example Series",
            State = ArrStates.Downloaded,
            Path = "/tv/Example Series",
            SizeOnDisk = 100,
            EpisodeFileCount = 8,
            SyncedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var settings = CreateSettings(sonarr: true);
        var services = CreateServices(db, settings);

        var result = await services.Delete.DeleteAsync(title.Id, deleteFiles: false, addImportExclusion: false);
        result.CanAdd.ShouldBeTrue();
        result.State.ShouldBe("notInLibrary");
        (await db.ArrLibraryItems.CountAsync()).ShouldBe(0);
        (await db.Titles.CountAsync()).ShouldBe(1);
        var audit = await db.ArrAuditLogs.SingleAsync();
        audit.Action.ShouldBe(ArrAuditActions.Delete);
        audit.Outcome.ShouldBe(ArrAuditOutcomes.Success);
    }

    [Fact]
    public async Task RadarrClient_AddAsync_posts_lookup_payload()
    {
        MapFixture("/api/v3/movie/lookup", "radarr/movie-lookup.json");
        MapPostFixture("/api/v3/movie", "radarr/movie-added.json");

        var settings = CreateSettings(sonarr: false, radarr: true);
        var http = new HttpClient { BaseAddress = new Uri(_server.Urls[0]) };
        var client = new RadarrClient(http, settings, NullLogger<RadarrClient>.Instance);

        var added = await client.AddAsync(new RadarrAddRequest(
            TmdbId: 603,
            QualityProfileId: 1,
            RootFolderPath: "/movies",
            Monitored: true,
            Tags: [],
            MinimumAvailability: "released",
            SearchForMovie: true));

        added.ArrId.ShouldBe(42);
        added.TmdbId.ShouldBe(603);
    }

    private Services CreateServices(PickarrDbContext db, MemorySettingsStore settings)
    {
        var http = new HttpClient { BaseAddress = new Uri(_server.Urls[0]) };
        var sonarr = new SonarrClient(http, settings, NullLogger<SonarrClient>.Instance);
        var radarr = new RadarrClient(http, settings, NullLogger<RadarrClient>.Instance);
        var factory = new ArrClientFactory(sonarr, radarr, settings);
        var syncRuns = new EfSyncRunStore(db, new SystemClock(), new TestSettingsStore());
        var sync = new ArrLibrarySync(db, factory, new SystemClock(), syncRuns, NullLogger<ArrLibrarySync>.Instance);
        var titles = new TitleQueryService(db, settings, new SystemClock());
        var add = new ArrAddService(db, factory, sync, titles, new SystemClock());
        var delete = new ArrDeleteService(db, factory, sync, titles, new SystemClock());
        return new Services(add, delete);
    }

    private sealed record Services(IArrAddService Add, IArrDeleteService Delete);

    private static async Task<Title> SeedSeriesAsync(PickarrDbContext db)
    {
        var title = new Title
        {
            MediaType = MediaTypes.Series,
            Category = TitleCategories.Series,
            TvdbId = 121361,
            TmdbId = 1396,
            TmdbType = "tv",
            OriginalTitle = "Example Series",
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Titles.Add(title);
        await db.SaveChangesAsync();
        return title;
    }

    private MemorySettingsStore CreateSettings(bool sonarr, bool radarr = false)
    {
        var settings = new MemorySettingsStore();
        if (sonarr)
        {
            settings.Set(SettingsKeys.SonarrUrl, JsonSerializer.Serialize(_server.Urls[0]));
            settings.Set(SettingsKeys.SonarrApiKey, JsonSerializer.Serialize("key"), secret: true);
        }

        if (radarr)
        {
            settings.Set(SettingsKeys.RadarrUrl, JsonSerializer.Serialize(_server.Urls[0]));
            settings.Set(SettingsKeys.RadarrApiKey, JsonSerializer.Serialize("key"), secret: true);
        }

        return settings;
    }

    private void MapFixture(string path, string relative)
    {
        var body = File.ReadAllText(Path.Combine(_fixtureRoot, relative));
        MapJson(path, body);
    }

    private void MapJson(string path, string body)
    {
        _server
            .Given(Request.Create().WithPath(path).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
    }

    private void MapPostFixture(string path, string relative)
    {
        var body = File.ReadAllText(Path.Combine(_fixtureRoot, relative));
        _server
            .Given(Request.Create().WithPath(path).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));
    }

    private void MapDeleteOk(string path)
    {
        _server
            .Given(Request.Create().WithPath(path).UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(200));
    }

    private static async Task<PickarrDbContext> CreateDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickarrDbContext>().UseSqlite(connection).Options;
        var db = new PickarrDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private sealed class MemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, (string Json, bool Secret)> _values = new(StringComparer.Ordinal);

        public void Set(string key, string valueJson, bool secret = false) =>
            _values[key] = (valueJson, secret);

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

    private static string FindFixtureRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "fixtures");
            if (Directory.Exists(Path.Combine(candidate, "sonarr")))
            {
                return candidate;
            }

            candidate = Path.Combine(dir.FullName, "fixtures");
            if (Directory.Exists(Path.Combine(candidate, "sonarr")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("fixtures folder not found");
    }
}
