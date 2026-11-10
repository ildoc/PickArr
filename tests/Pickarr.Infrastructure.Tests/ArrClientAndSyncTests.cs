using System.Text.Json;
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

public sealed class ArrClientAndSyncTests : IAsyncLifetime
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
    public async Task SonarrClient_probe_returns_profiles_for_the_credentials_under_test()
    {
        _server.Reset();
        MapFixture("/api/v3/system/status", "sonarr/system-status.json");
        MapFixture("/api/v3/qualityprofile", "sonarr/qualityprofiles.json");
        MapFixture("/api/v3/rootfolder", "sonarr/rootfolders.json");

        var http = new HttpClient { BaseAddress = new Uri(_server.Urls[0]) };
        var client = new SonarrClient(http, new MemorySettingsStore(), NullLogger<SonarrClient>.Instance);

        var probe = await client.ProbeAsync(_server.Urls[0], "one-off-key");

        probe.Version.ShouldContain("4.");
        probe.AppName.ShouldBe("Sonarr");
        probe.QualityProfiles.Select(p => p.Name).ShouldBe(["Any", "HD-1080p"]);
        probe.RootFolders.Single().Path.ShouldBe("/tv");
    }

    [Fact]
    public async Task RadarrClient_probe_returns_profiles_for_the_credentials_under_test()
    {
        _server.Reset();
        MapFixture("/api/v3/system/status", "radarr/system-status.json");
        MapFixture("/api/v3/qualityprofile", "radarr/qualityprofiles.json");
        MapFixture("/api/v3/rootfolder", "radarr/rootfolders.json");

        var http = new HttpClient { BaseAddress = new Uri(_server.Urls[0]) };
        var client = new RadarrClient(http, new MemorySettingsStore(), NullLogger<RadarrClient>.Instance);

        var probe = await client.ProbeAsync(_server.Urls[0], "one-off-key");

        probe.AppName.ShouldBe("Radarr");
        probe.QualityProfiles.ShouldContain(p => p.Name == "HD-1080p");
        probe.RootFolders.Single().Path.ShouldBe("/movies");
    }

    [Fact]
    public async Task SonarrClient_parses_library_and_queue_fixtures()
    {
        MapFixture("/api/v3/system/status", "sonarr/system-status.json");
        MapFixture("/api/v3/series", "sonarr/series-library.json");
        MapFixture("/api/v3/queue", "sonarr/queue-page1.json");
        MapFixture("/api/v3/qualityprofile", "sonarr/qualityprofiles.json");
        MapFixture("/api/v3/rootfolder", "sonarr/rootfolders.json");
        MapJson("/api/v3/tag", "[]");

        var settings = CreateSettings(sonarr: true);
        var http = new HttpClient { BaseAddress = new Uri(_server.Urls[0]) };
        var client = new SonarrClient(http, settings, NullLogger<SonarrClient>.Instance);

        var info = await client.GetSystemInfoAsync();
        info.Version.ShouldContain("4.");
        var library = await client.GetLibraryAsync();
        library.Count.ShouldBe(2);
        library[0].Seasons.Count.ShouldBe(3);
        var queue = await client.GetQueueAsync();
        queue.ShouldContain(q => q.ArrItemId == 55);
        var options = await client.GetOptionsAsync();
        options.DefaultQualityProfileId.ShouldBe(1);
        options.DefaultRootFolderPath.ShouldBe("/tv");
    }

    [Fact]
    public async Task ArrLibrarySync_upserts_and_keeps_cache_on_failure()
    {
        MapFixture("/api/v3/series", "sonarr/series-library.json");
        MapFixture("/api/v3/queue", "sonarr/queue-page1.json");

        await using var db = await CreateDbAsync();
        var settings = CreateSettings(sonarr: true);
        db.Titles.Add(new Title
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
        });
        await db.SaveChangesAsync();

        var http = new HttpClient { BaseAddress = new Uri(_server.Urls[0]) };
        var sonarr = new SonarrClient(http, settings, NullLogger<SonarrClient>.Instance);
        var radarr = new RadarrClient(http, settings, NullLogger<RadarrClient>.Instance);
        var factory = new ArrClientFactory(sonarr, radarr, settings);
        var syncRuns = new EfSyncRunStore(db, new SystemClock());
        var sync = new ArrLibrarySync(db, factory, new SystemClock(), syncRuns, NullLogger<ArrLibrarySync>.Instance);

        await sync.SyncAsync(ArrKind.Sonarr);
        (await db.ArrLibraryItems.CountAsync()).ShouldBe(2);
        var matched = await db.ArrLibraryItems.SingleAsync(a => a.ArrId == 55);
        matched.TitleId.ShouldNotBeNull();
        matched.State.ShouldBe(ArrStates.Downloading);

        _server.Reset();
        _server
            .Given(Request.Create().WithPath("/api/v3/series").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        await Should.ThrowAsync<ArrClientException>(() => sync.SyncAsync(ArrKind.Sonarr));
        (await db.ArrLibraryItems.CountAsync()).ShouldBe(2);
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
