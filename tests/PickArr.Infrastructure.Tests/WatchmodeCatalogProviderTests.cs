using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PickArr.Core.Abstractions;
using PickArr.Core.Catalog;
using PickArr.Core.Settings;
using PickArr.Infrastructure;
using PickArr.Infrastructure.Catalog.Watchmode;
using PickArr.Infrastructure.Persistence;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace PickArr.Infrastructure.Tests;

public sealed class WatchmodeCatalogProviderTests : IAsyncLifetime
{
    private readonly WireMockServer _server = WireMockServer.Start();
    private readonly string _fixtureRoot = FindFixtureRoot();
    private ServiceProvider? _services;
    private SqliteConnection? _connection;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _server.Stop();
        _server.Dispose();
    }

    [Fact]
    public async Task ListTitlesAsync_parses_fixture_and_sends_api_key_header()
    {
        var body = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "list-titles-page1.json"));
        _server
            .Given(Request.Create().WithPath("/v1/list-titles/").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("X-Account-Quota", "2500")
                .WithHeader("X-Account-Quota-Used", "42")
                .WithBody(body));

        var provider = await CreateProviderAsync(apiKey: "test-key-secret");

        var page = await provider.ListTitlesAsync(new CatalogListRequest(
            ProviderExternalId: "203",
            CountryCode: "US",
            Types: [CatalogEntryType.Movie, CatalogEntryType.TvSeries, CatalogEntryType.TvMiniseries, CatalogEntryType.TvSpecial],
            Sort: CatalogSort.TitleAsc,
            Page: 1,
            PageSize: 250));

        page.Page.ShouldBe(1);
        page.TotalPages.ShouldBe(25);
        page.TotalResults.ShouldBe(6120);
        page.CreditsUsed.ShouldBe(1);
        page.Entries.Count.ShouldBe(4);
        page.Entries[0].ExternalId.ShouldBe("1000001");
        page.Entries[0].Type.ShouldBe(CatalogEntryType.Movie);
        page.Entries[1].Type.ShouldBe(CatalogEntryType.TvSeries);
        page.Entries[2].Type.ShouldBe(CatalogEntryType.TvSpecial);
        page.Entries[3].Type.ShouldBe(CatalogEntryType.TvMiniseries);

        var log = _server.LogEntries.ShouldHaveSingleItem();
        log.RequestMessage.Headers.ShouldNotBeNull();
        log.RequestMessage.Headers!["X-API-Key"].ShouldContain("test-key-secret");
        log.RequestMessage.RawQuery.ShouldNotContain("apiKey=");
        log.RequestMessage.Query!["sort_by"].ShouldContain("title_asc");
        log.RequestMessage.Query["regions"].ShouldContain("US");
        log.RequestMessage.Query["source_ids"].ShouldContain("203");

        var usages = await GetApiUsagesAsync();
        usages.Count.ShouldBe(1);
        usages[0].Service.ShouldBe("watchmode");
        usages[0].Endpoint.ShouldBe("/list-titles/");
        usages[0].Credits.ShouldBe(1);
        usages[0].QuotaUsed.ShouldBe(42);
        usages[0].QuotaLimit.ShouldBe(2500);
        usages[0].StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task GetAvailableProvidersAsync_and_GetCountriesAsync_use_sources_fixture()
    {
        var sources = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "sources-netflix.json"));
        _server
            .Given(Request.Create().WithPath("/v1/sources/").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("X-Account-Quota-Used", "13")
                .WithBody(sources));

        var provider = await CreateProviderAsync();

        var providers = await provider.GetAvailableProvidersAsync();
        providers.Count.ShouldBe(1);
        providers[0].ExternalId.ShouldBe("203");
        providers[0].Name.ShouldBe("Netflix");
        providers[0].Regions.ShouldContain("IT");
        providers[0].Regions.ShouldContain("US");

        var countries = await provider.GetCountriesAsync("203");
        countries.ShouldContain("IT");
        countries.ShouldContain("US");

        var usages = await GetApiUsagesAsync();
        usages.Count.ShouldBeGreaterThanOrEqualTo(1);
        usages.ShouldContain(u => u.Endpoint == "/sources/" && u.Credits == 1);
    }

    [Fact]
    public async Task GetLeavingAsync_parses_preview_fixture()
    {
        var body = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "title-leaving-dates-preview.json"));
        _server
            .Given(Request.Create().WithPath("/v1/title-leaving-dates/").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

        var provider = await CreateProviderAsync();
        var leaving = await provider.GetLeavingAsync(new CatalogSourceRef("203", "US"));

        leaving.Count.ShouldBe(2);
        leaving[0].LeavingDate.ShouldBe(new DateOnly(2026, 11, 15));
        leaving[1].Season.ShouldBe(2);
    }

    [Fact]
    public async Task ListTitlesAsync_throws_WatchmodeQuotaExceededException_on_429()
    {
        var body = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "error-quota-exceeded.json"));
        _server
            .Given(Request.Create().WithPath("/v1/list-titles/").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

        var provider = await CreateProviderAsync();

        await Should.ThrowAsync<WatchmodeQuotaExceededException>(() =>
            provider.ListTitlesAsync(new CatalogListRequest(
                "203", "US", [CatalogEntryType.Movie], CatalogSort.TitleAsc, 1, 250)));

        var usages = await GetApiUsagesAsync();
        usages.ShouldContain(u => u.StatusCode == 429 && u.Endpoint == "/list-titles/");
    }

    [Fact]
    public async Task GetChangedTitlesAsync_requires_paid_plan()
    {
        var provider = await CreateProviderAsync(plan: "free");
        await Should.ThrowAsync<NotSupportedException>(() =>
            provider.GetChangedTitlesAsync(new CatalogSourceRef("203", "US"), new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 4)));
    }

    [Fact]
    public async Task ProviderResolver_resolves_watchmode()
    {
        await CreateProviderAsync();
        var resolver = _services!.GetRequiredService<IProviderResolver>();
        resolver.Resolve("watchmode").Type.ShouldBe("watchmode");
        Should.Throw<CatalogProviderNotSupportedException>(() => resolver.Resolve("unknown"));
    }

    private async Task<ICatalogProvider> CreateProviderAsync(string apiKey = "test-key", string plan = "free")
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var dataPath = Path.Combine(Path.GetTempPath(), "PickArr-wm-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataPath);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PickArr:DataPath"] = dataPath,
                ["PickArr:Watchmode:BaseUrl"] = _server.Urls[0] + "/v1/",
                ["PickArr:Watchmode:RequestsPerSecond"] = "1000",
                ["PickArr:Watchmode:DisableResilience"] = "true",
                ["PickArr:Hangfire:Storage"] = "Memory",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureAll<Microsoft.Extensions.Http.HttpClientFactoryOptions>(o =>
            o.HandlerLifetime = TimeSpan.FromSeconds(1));
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IClock, FixedClock>();
        services.AddSingleton<IApiUsageSyncRunAccessor, PickArr.Infrastructure.Catalog.ApiUsageSyncRunAccessor>();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddDbContext<PickArrDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<ISettingsStore, PickArr.Infrastructure.Settings.ProtectedSettingsStore>();
        services.AddPickArrWatchmode(config);

        _services = services.BuildServiceProvider();

        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
            await db.Database.EnsureCreatedAsync();
            var store = scope.ServiceProvider.GetRequiredService<ISettingsStore>();
            await store.SetAsync(SettingsKeys.WatchmodeApiKey, JsonSerializer.Serialize(apiKey), isSecret: true);
            await store.SetAsync(SettingsKeys.WatchmodePlan, JsonSerializer.Serialize(plan), isSecret: false);
        }

        return _services.GetRequiredService<ICatalogProvider>();
    }

    private async Task<List<PickArr.Core.Domain.Entities.ApiUsage>> GetApiUsagesAsync()
    {
        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        return await db.ApiUsages.OrderBy(x => x.Timestamp).ToListAsync();
    }

    private static string FindFixtureRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "fixtures", "watchmode");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.Combine(dir.FullName, "fixtures", "watchmode");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate tests/fixtures/watchmode");
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    }
}
