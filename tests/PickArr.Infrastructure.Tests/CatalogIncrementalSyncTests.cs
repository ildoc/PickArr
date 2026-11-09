using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PickArr.Core.Abstractions;
using PickArr.Core.Catalog;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Catalog;
using PickArr.Infrastructure.Catalog.Watchmode;
using PickArr.Infrastructure.Persistence;
using PickArr.Infrastructure.Settings;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace PickArr.Infrastructure.Tests;

public sealed class CatalogIncrementalSyncTests : IAsyncLifetime
{
    private readonly WireMockServer _server = WireMockServer.Start();
    private ServiceProvider? _services;
    private SqliteConnection? _connection;
    private int _sourceId;

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
    public async Task RunAsync_fetches_pages_upserts_and_succeeds()
    {
        var fixtureRoot = FindFixtureRoot();
        var body = await File.ReadAllTextAsync(Path.Combine(fixtureRoot, "list-titles-page1.json"));
        _server
            .Given(Request.Create().WithPath("/v1/list-titles/").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("X-Account-Quota", "2500")
                .WithHeader("X-Account-Quota-Used", "10")
                .WithBody(body));

        await BuildServicesAsync(incrementalPages: 1, baselineCompleted: true);
        var sync = _services!.GetRequiredService<ICatalogIncrementalSync>();

        var result = await sync.RunAsync(_sourceId, isManual: false);
        result.Status.ShouldBe(SyncRunStatuses.Succeeded);
        result.PagesFetched.ShouldBe(1);
        result.CreditsUsed.ShouldBe(1);

        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        (await db.Titles.CountAsync()).ShouldBe(4);
        (await db.Availabilities.CountAsync()).ShouldBe(4);
        (await db.AvailabilityEvents.CountAsync(e => e.Type == AvailabilityEventTypes.Added)).ShouldBe(4);
    }

    [Fact]
    public async Task RunAsync_pauses_when_budget_exhausted()
    {
        var fixtureRoot = FindFixtureRoot();
        var body = await File.ReadAllTextAsync(Path.Combine(fixtureRoot, "list-titles-page1.json"));
        _server
            .Given(Request.Create().WithPath("/v1/list-titles/").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

        await BuildServicesAsync(incrementalPages: 3, baselineCompleted: false, maxCreditsPerRun: 1);
        var sync = _services!.GetRequiredService<ICatalogIncrementalSync>();

        var result = await sync.RunAsync(_sourceId, isManual: false);
        result.Status.ShouldBe(SyncRunStatuses.Paused);
        result.CheckpointJson.ShouldNotBeNull();
        result.PagesFetched.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_records_the_first_import_as_new_titles()
    {
        var fixtureRoot = FindFixtureRoot();
        var body = await File.ReadAllTextAsync(Path.Combine(fixtureRoot, "list-titles-page1.json"));
        _server
            .Given(Request.Create().WithPath("/v1/list-titles/").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("X-Account-Quota", "2500")
                .WithHeader("X-Account-Quota-Used", "10")
                .WithBody(body));

        await BuildServicesAsync(incrementalPages: 1, baselineCompleted: false);
        var sync = _services!.GetRequiredService<ICatalogIncrementalSync>();

        var result = await sync.RunAsync(_sourceId, isManual: false);
        result.Status.ShouldBe(SyncRunStatuses.Succeeded);

        var list = _server.LogEntries.Last(entry => entry.RequestMessage.Path.Contains("list-titles", StringComparison.Ordinal));
        list.RequestMessage.Query!["sort_by"].ShouldContain("release_date_desc");

        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var rows = await db.Availabilities.ToListAsync();
        rows.ShouldNotBeEmpty();
        rows.ShouldAllBe(row => !row.IsBaseline && row.AddedAt != null);
    }

    [Fact]
    public async Task RunAsync_resumes_from_checkpoint_page()
    {
        var fixtureRoot = FindFixtureRoot();
        var body = await File.ReadAllTextAsync(Path.Combine(fixtureRoot, "list-titles-page1.json"));
        _server
            .Given(Request.Create().WithPath("/v1/list-titles/").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("X-Account-Quota", "2500")
                .WithHeader("X-Account-Quota-Used", "10")
                .WithBody(body));

        await BuildServicesAsync(incrementalPages: 3, baselineCompleted: true);
        var sync = _services!.GetRequiredService<ICatalogIncrementalSync>();

        var result = await sync.RunAsync(
            _sourceId,
            isManual: false,
            resumeCheckpointJson: """{"page":3}""");
        result.Status.ShouldBe(SyncRunStatuses.Succeeded);
        result.PagesFetched.ShouldBe(3);
        result.CreditsUsed.ShouldBe(1);

        var pages = _server.LogEntries
            .Where(e => e.RequestMessage.Path.Contains("list-titles", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.RequestMessage.Query!["page"].First())
            .ToList();
        pages.ShouldBe(["3"]);
    }

    private async Task BuildServicesAsync(
        int incrementalPages,
        bool baselineCompleted,
        int maxCreditsPerRun = 80)
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var dataPath = Path.Combine(Path.GetTempPath(), "PickArr-incr-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataPath);

        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PickArr:DataPath"] = dataPath,
                ["PickArr:Watchmode:BaseUrl"] = _server.Urls[0] + "/v1/",
                ["PickArr:Watchmode:RequestsPerSecond"] = "1000",
                ["PickArr:Watchmode:DisableResilience"] = "true",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureAll<Microsoft.Extensions.Http.HttpClientFactoryOptions>(o =>
            o.HandlerLifetime = TimeSpan.FromSeconds(1));
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(config);
        services.AddSingleton<IClock, FixedClock>();
        services.AddSingleton<IApiUsageSyncRunAccessor, ApiUsageSyncRunAccessor>();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddDbContext<PickArrDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<ISettingsStore, ProtectedSettingsStore>();
        services.AddScoped<ISyncRunStore, EfSyncRunStore>();
        services.AddScoped<IQuotaUsageReader, WatchmodeQuotaUsageReader>();
        services.AddScoped<ICatalogUpserter, CatalogUpserter>();
        services.AddSingleton<WatchmodeQuotaObservation>();
        services.AddScoped<ICatalogIncrementalSync, CatalogIncrementalSync>();
        services.AddScoped<ICreditBudget, CreditBudgetService>();
        services.AddPickArrWatchmode(config);

        _services = services.BuildServiceProvider();

        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        await db.Database.EnsureCreatedAsync();

        var provider = new Provider
        {
            Key = "netflix",
            DisplayName = "Netflix",
            ProviderType = "watchmode",
            ExternalId = "203",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();

        var source = new CatalogSource
        {
            ProviderId = provider.Id,
            CountryCode = "US",
            MetadataLanguage = "en-US",
            IsEnabled = true,
            IncrementalPages = incrementalPages,
            BaselineCompletedAt = baselineCompleted ? DateTime.UtcNow : null,
        };
        db.CatalogSources.Add(source);
        await db.SaveChangesAsync();
        _sourceId = source.Id;

        var store = scope.ServiceProvider.GetRequiredService<ISettingsStore>();
        await store.SetAsync(SettingsKeys.WatchmodeApiKey, JsonSerializer.Serialize("test-key"), true);
        await store.SetAsync(SettingsKeys.WatchmodePlan, JsonSerializer.Serialize("free"), false);
        await store.SetAsync(SettingsKeys.WatchmodeQuota, "2500", false);
        await store.SetAsync(SettingsKeys.WatchmodeReservePercent, "10", false);
        await store.SetAsync(SettingsKeys.WatchmodeMaxCreditsPerRun, maxCreditsPerRun.ToString(), false);
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
