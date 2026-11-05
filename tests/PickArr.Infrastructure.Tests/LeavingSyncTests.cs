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

public sealed class LeavingSyncTests : IAsyncLifetime
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
    public async Task RunAsync_sets_leaving_dates_and_announces_events()
    {
        var fixtureRoot = FindFixtureRoot();
        var body = await File.ReadAllTextAsync(Path.Combine(fixtureRoot, "title-leaving-dates-preview.json"));
        _server
            .Given(Request.Create().WithPath("/v1/title-leaving-dates/").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("X-Account-Quota", "2500")
                .WithHeader("X-Account-Quota-Used", "12")
                .WithBody(body));

        await BuildServicesAsync(plan: "free");
        var sync = _services!.GetRequiredService<ILeavingSync>();

        var result = await sync.RunAsync(_sourceId, isManual: true);
        result.Status.ShouldBe(SyncRunStatuses.Succeeded);
        result.PagesFetched.ShouldBe(1);
        result.CreditsUsed.ShouldBe(1);
        result.TitlesUpdated.ShouldBe(1); // only US fixture id 1000001 is on this source

        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var alpha = await db.Availabilities
            .Include(a => a.Title)
            .SingleAsync(a => a.Title!.WatchmodeId == 1000001);
        alpha.LeavingDate.ShouldBe(new DateOnly(2026, 11, 15));
        (await db.AvailabilityEvents.CountAsync(e => e.Type == AvailabilityEventTypes.LeavingAnnounced))
            .ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_free_plan_does_not_clear_dates_absent_from_preview()
    {
        var fixtureRoot = FindFixtureRoot();
        var body = await File.ReadAllTextAsync(Path.Combine(fixtureRoot, "title-leaving-dates-preview.json"));
        _server
            .Given(Request.Create().WithPath("/v1/title-leaving-dates/").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

        await BuildServicesAsync(plan: "free", seedExtraLeaving: true);
        var sync = _services!.GetRequiredService<ILeavingSync>();

        var result = await sync.RunAsync(_sourceId, isManual: false);
        result.Status.ShouldBe(SyncRunStatuses.Succeeded);

        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var orphan = await db.Availabilities
            .Include(a => a.Title)
            .SingleAsync(a => a.Title!.WatchmodeId == 1999999);
        orphan.LeavingDate.ShouldBe(new DateOnly(2026, 12, 1));
        (await db.AvailabilityEvents.CountAsync(e => e.Type == AvailabilityEventTypes.LeavingCleared))
            .ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_paid_plan_clears_dates_absent_from_response()
    {
        var fixtureRoot = FindFixtureRoot();
        var body = await File.ReadAllTextAsync(Path.Combine(fixtureRoot, "title-leaving-dates-preview.json"));
        _server
            .Given(Request.Create().WithPath("/v1/title-leaving-dates/").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

        await BuildServicesAsync(plan: "paid", seedExtraLeaving: true);
        var sync = _services!.GetRequiredService<ILeavingSync>();

        var result = await sync.RunAsync(_sourceId, isManual: false);
        result.Status.ShouldBe(SyncRunStatuses.Succeeded);

        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var orphan = await db.Availabilities
            .Include(a => a.Title)
            .SingleAsync(a => a.Title!.WatchmodeId == 1999999);
        orphan.LeavingDate.ShouldBeNull();
        (await db.AvailabilityEvents.CountAsync(e => e.Type == AvailabilityEventTypes.LeavingCleared))
            .ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_pauses_when_budget_exhausted()
    {
        var fixtureRoot = FindFixtureRoot();
        var body = await File.ReadAllTextAsync(Path.Combine(fixtureRoot, "title-leaving-dates-preview.json"));
        _server
            .Given(Request.Create().WithPath("/v1/title-leaving-dates/").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(body));

        await BuildServicesAsync(plan: "free", maxCreditsPerRun: 0);
        var sync = _services!.GetRequiredService<ILeavingSync>();

        var result = await sync.RunAsync(_sourceId, isManual: false);
        result.Status.ShouldBe(SyncRunStatuses.Paused);
        _server.LogEntries.ShouldBeEmpty();
    }

    private async Task BuildServicesAsync(
        string plan,
        bool seedExtraLeaving = false,
        int maxCreditsPerRun = 80)
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var dataPath = Path.Combine(Path.GetTempPath(), "PickArr-leaving-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataPath);

        var config = new ConfigurationBuilder()
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
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IClock, FixedClock>();
        services.AddSingleton<IApiUsageSyncRunAccessor, ApiUsageSyncRunAccessor>();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddDbContext<PickArrDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<ISettingsStore, ProtectedSettingsStore>();
        services.AddScoped<ISyncRunStore, EfSyncRunStore>();
        services.AddScoped<IQuotaUsageReader, WatchmodeQuotaUsageReader>();
        services.AddScoped<ILeavingSync, LeavingSync>();
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
            BaselineCompletedAt = DateTime.UtcNow,
        };
        db.CatalogSources.Add(source);
        await db.SaveChangesAsync();
        _sourceId = source.Id;

        var now = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
        db.Titles.Add(new Title
        {
            WatchmodeId = 1000001,
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = "Example Movie Alpha",
            MetadataStatus = MetadataStatuses.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        });
        db.Titles.Add(new Title
        {
            WatchmodeId = 1000002,
            MediaType = MediaTypes.Series,
            Category = TitleCategories.Series,
            OriginalTitle = "Example Series Beta",
            MetadataStatus = MetadataStatuses.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        });
        if (seedExtraLeaving)
        {
            db.Titles.Add(new Title
            {
                WatchmodeId = 1999999,
                MediaType = MediaTypes.Movie,
                Category = TitleCategories.Movie,
                OriginalTitle = "Orphan Leaving",
                MetadataStatus = MetadataStatuses.Pending,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync();

        foreach (var title in db.Titles.Local)
        {
            db.Availabilities.Add(new Availability
            {
                TitleId = title.Id,
                CatalogSourceId = _sourceId,
                FirstSeenAt = now,
                LastSeenAt = now,
                AddedAt = now,
                IsBaseline = true,
                LeavingDate = title.WatchmodeId == 1999999 ? new DateOnly(2026, 12, 1) : null,
            });
        }

        await db.SaveChangesAsync();

        var store = scope.ServiceProvider.GetRequiredService<ISettingsStore>();
        await store.SetAsync(SettingsKeys.WatchmodeApiKey, JsonSerializer.Serialize("test-key"), true);
        await store.SetAsync(SettingsKeys.WatchmodePlan, JsonSerializer.Serialize(plan), false);
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
