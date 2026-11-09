using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Settings;
using PickArr.Infrastructure;
using PickArr.Infrastructure.Catalog;
using PickArr.Infrastructure.Metadata;
using PickArr.Infrastructure.Persistence;
using PickArr.Infrastructure.Settings;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace PickArr.Infrastructure.Tests;

public sealed class EnrichmentServiceTests : IAsyncLifetime
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
    public async Task Enrich_movie_sets_Ready_localizations_and_records_ApiUsage()
    {
        var movie = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "movie-550-en.json"));
        var config = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "configuration.json"));
        _server.Given(Request.Create().WithPath("/3/movie/550").UsingGet())
            .RespondWith(Json(movie));
        _server.Given(Request.Create().WithPath("/3/configuration").UsingGet())
            .RespondWith(Json(config));

        var titleId = await SeedTitleAsync(tmdbId: 550, tmdbType: "movie", language: "en-US");
        var enrichment = _services!.GetRequiredService<IEnrichmentService>();
        await enrichment.EnrichTitleAsync(titleId);

        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var title = await db.Titles.Include(t => t.Localizations).Include(t => t.TitleGenres)
            .SingleAsync(t => t.Id == titleId);
        title.MetadataStatus.ShouldBe(MetadataStatuses.Ready);
        title.ImdbId.ShouldBe("tt0137523");
        title.PosterPath.ShouldNotBeNull();
        title.Localizations.ShouldContain(l => l.Language == "en-US" && l.Name == "Fight Club");
        title.TitleGenres.Count.ShouldBeGreaterThan(0);
        title.Category.ShouldBe(TitleCategories.Movie);

        (await db.ApiUsages.CountAsync(u => u.Service == "tmdb")).ShouldBeGreaterThan(0);

        var urls = scope.ServiceProvider.GetRequiredService<ITmdbImageUrlBuilder>();
        var poster = await urls.BuildPosterUrlAsync(title.PosterPath);
        poster.ShouldStartWith("https://image.tmdb.org/t/p/w342/");
    }

    [Fact]
    public async Task Enrich_tv_maps_tvdb_and_stand_up_category()
    {
        var tv = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "tv-1396-en.json"));
        var standup = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "tv-standup-keywords.json"));
        _server.Given(Request.Create().WithPath("/3/tv/1396").UsingGet()).RespondWith(Json(tv));
        _server.Given(Request.Create().WithPath("/3/tv/900001").UsingGet()).RespondWith(Json(standup));

        var seriesId = await SeedTitleAsync(1396, "tv", "en-US");
        var standupId = await SeedTitleAsync(900001, "tv", "en-US", reuseDb: true);

        var enrichment = _services!.GetRequiredService<IEnrichmentService>();
        await enrichment.EnrichTitleAsync(seriesId);
        await enrichment.EnrichTitleAsync(standupId);

        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var series = await db.Titles.SingleAsync(t => t.Id == seriesId);
        series.TvdbId.ShouldBe(81189);
        series.MetadataStatus.ShouldBe(MetadataStatuses.Ready);

        var special = await db.Titles.SingleAsync(t => t.Id == standupId);
        special.Category.ShouldBe(TitleCategories.StandUp);
        special.MediaType.ShouldBe(MediaTypes.Series);
    }

    [Fact]
    public async Task Enrich_persists_keywords_credits_and_cast_names()
    {
        var movie = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "movie-550-en.json"));
        _server.Given(Request.Create().WithPath("/3/movie/550").UsingGet()).RespondWith(Json(movie));

        var titleId = await SeedTitleAsync(550, "movie", "en-US");
        await _services!.GetRequiredService<IEnrichmentService>().EnrichTitleAsync(titleId);

        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var title = await db.Titles
            .Include(t => t.TitleKeywords)
            .Include(t => t.TitleCredits)
            .SingleAsync(t => t.Id == titleId);

        title.TitleKeywords.ShouldContain(tk => tk.KeywordId == 825);
        title.TitleCredits.ShouldContain(tc => tc.PersonId == 819 && tc.Role == CreditRoles.Cast);
        title.TitleCredits.ShouldContain(tc => tc.PersonId == 7467 && tc.Role == CreditRoles.Director);
        JsonSerializer.Deserialize<List<string>>(title.OriginCountries!)!.ShouldContain("US");

        var cast = JsonSerializer.Deserialize<List<string>>(title.CastJson!);
        cast.ShouldNotBeNull();
        cast.ShouldContain("Edward Norton");
        cast.ShouldContain("Brad Pitt");
    }

    [Fact]
    public async Task Enrich_tv_persists_created_by_as_creator()
    {
        var tv = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "tv-1396-en.json"));
        _server.Given(Request.Create().WithPath("/3/tv/1396").UsingGet()).RespondWith(Json(tv));

        var titleId = await SeedTitleAsync(1396, "tv", "en-US");
        await _services!.GetRequiredService<IEnrichmentService>().EnrichTitleAsync(titleId);

        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var credits = await db.TitleCredits.Where(c => c.TitleId == titleId).ToListAsync();
        credits.ShouldContain(c => c.PersonId == 66633 && c.Role == CreditRoles.Creator);
        credits.ShouldContain(c => c.PersonId == 17419 && c.Role == CreditRoles.Cast);
    }

    [Fact]
    public async Task Enrich_fetches_en_US_even_when_source_overview_is_full()
    {
        var itFull = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "movie-550-it-full.json"));
        var en = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "movie-550-en.json"));
        _server.Given(Request.Create().WithPath("/3/movie/550").WithParam("language", "it-IT").UsingGet())
            .RespondWith(Json(itFull));
        _server.Given(Request.Create().WithPath("/3/movie/550").WithParam("language", "en-US").UsingGet())
            .RespondWith(Json(en));

        var titleId = await SeedTitleAsync(550, "movie", "it-IT");
        await _services!.GetRequiredService<IEnrichmentService>().EnrichTitleAsync(titleId);

        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var locs = await db.TitleLocalizations.Where(l => l.TitleId == titleId).ToListAsync();
        locs.Count.ShouldBe(2);
        locs.ShouldContain(l => l.Language == "it-IT" && !string.IsNullOrWhiteSpace(l.Overview));
        locs.ShouldContain(l =>
            l.Language == "en-US"
            && l.Overview != null
            && l.Overview.Contains("insomniac", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Feature_backfill_reenriches_ready_titles_missing_keywords()
    {
        var movie = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "movie-550-en.json"));
        _server.Given(Request.Create().WithPath("/3/movie/550").UsingGet()).RespondWith(Json(movie));

        var titleId = await SeedTitleAsync(550, "movie", "en-US");
        await using (var scope = _services!.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
            var title = await db.Titles.SingleAsync(t => t.Id == titleId);
            title.MetadataStatus = MetadataStatuses.Ready;
            title.MetadataFetchedAt = DateTime.UtcNow;
            title.OriginalTitle = "Fight Club";
            await db.SaveChangesAsync();
        }

        var result = await _services!.GetRequiredService<IEnrichmentService>()
            .EnrichFeatureBackfillBatchAsync(10);
        result.Processed.ShouldBe(1);
        result.Ready.ShouldBe(1);

        await using var verify = _services!.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<PickArrDbContext>();
        (await verifyDb.TitleKeywords.CountAsync(tk => tk.TitleId == titleId)).ShouldBeGreaterThan(0);
        (await verifyDb.TitleCredits.CountAsync(tc => tc.TitleId == titleId)).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Enrich_falls_back_to_en_US_when_overview_empty()
    {
        var itEmpty = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "movie-550-it-empty-overview.json"));
        var en = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "movie-550-en.json"));
        _server.Given(Request.Create().WithPath("/3/movie/550").WithParam("language", "it-IT").UsingGet())
            .RespondWith(Json(itEmpty));
        _server.Given(Request.Create().WithPath("/3/movie/550").WithParam("language", "en-US").UsingGet())
            .RespondWith(Json(en));

        var titleId = await SeedTitleAsync(550, "movie", "it-IT");
        await _services!.GetRequiredService<IEnrichmentService>().EnrichTitleAsync(titleId);

        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var locs = await db.TitleLocalizations.Where(l => l.TitleId == titleId).ToListAsync();
        locs.Count.ShouldBe(2);
        locs.ShouldContain(l => l.Language == "it-IT");
        locs.ShouldContain(l => l.Language == "en-US" && !string.IsNullOrWhiteSpace(l.Overview));
    }

    [Fact]
    public async Task Enrich_without_tmdb_uses_find_by_imdb()
    {
        var find = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "find-imdb.json"));
        var movie = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "movie-550-en.json"));
        _server.Given(Request.Create().WithPath("/3/find/tt0137523").UsingGet()).RespondWith(Json(find));
        _server.Given(Request.Create().WithPath("/3/movie/550").UsingGet()).RespondWith(Json(movie));

        var titleId = await SeedTitleAsync(tmdbId: null, tmdbType: null, language: "en-US", imdbId: "tt0137523");
        await _services!.GetRequiredService<IEnrichmentService>().EnrichTitleAsync(titleId);

        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var title = await db.Titles.SingleAsync(t => t.Id == titleId);
        title.TmdbId.ShouldBe(550);
        title.MetadataStatus.ShouldBe(MetadataStatuses.Ready);
    }

    [Fact]
    public async Task Enrich_marks_Unmapped_when_tmdb_returns_404_for_both_types()
    {
        _server.Given(Request.Create().WithPath("/3/movie/1727694").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));
        _server.Given(Request.Create().WithPath("/3/tv/1727694").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));

        var titleId = await SeedTitleAsync(1727694, "movie", "en-US");
        var result = await _services!.GetRequiredService<IEnrichmentService>()
            .EnrichPendingBatchAsync(limit: 10);

        result.Unmapped.ShouldBe(1);
        result.Failed.ShouldBe(0);
        result.Ready.ShouldBe(0);

        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var title = await db.Titles.SingleAsync(t => t.Id == titleId);
        title.MetadataStatus.ShouldBe(MetadataStatuses.Unmapped);
    }

    [Fact]
    public async Task Enrich_corrects_wrong_tmdb_type_when_alternate_endpoint_succeeds()
    {
        var tv = await File.ReadAllTextAsync(Path.Combine(_fixtureRoot, "tv-1396-en.json"));
        _server.Given(Request.Create().WithPath("/3/movie/1396").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404));
        _server.Given(Request.Create().WithPath("/3/tv/1396").UsingGet()).RespondWith(Json(tv));

        var titleId = await SeedTitleAsync(1396, "movie", "en-US");
        await _services!.GetRequiredService<IEnrichmentService>().EnrichTitleAsync(titleId);

        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var title = await db.Titles.SingleAsync(t => t.Id == titleId);
        title.MetadataStatus.ShouldBe(MetadataStatuses.Ready);
        title.TmdbType.ShouldBe("tv");
        title.MediaType.ShouldBe(MediaTypes.Series);
        title.TvdbId.ShouldBe(81189);
    }

    private static IResponseBuilder Json(string body) =>
        Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(body);

    private async Task<int> SeedTitleAsync(
        int? tmdbId,
        string? tmdbType,
        string language,
        string? imdbId = null,
        bool reuseDb = false)
    {
        if (!reuseDb || _services is null)
        {
            await BuildServicesAsync();
        }

        await using var scope = _services!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var provider = await db.Providers.FirstOrDefaultAsync(p => p.Key == "netflix");
        if (provider is null)
        {
            provider = new Provider
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
        }

        var source = await db.CatalogSources.FirstOrDefaultAsync(s => s.ProviderId == provider.Id && s.CountryCode == "US");
        if (source is null)
        {
            source = new CatalogSource
            {
                ProviderId = provider.Id,
                CountryCode = "US",
                MetadataLanguage = language,
                IsEnabled = true,
            };
            db.CatalogSources.Add(source);
            await db.SaveChangesAsync();
        }
        else if (!string.Equals(source.MetadataLanguage, language, StringComparison.Ordinal))
        {
            source.MetadataLanguage = language;
            await db.SaveChangesAsync();
        }

        var title = new Title
        {
            TmdbId = tmdbId,
            TmdbType = tmdbType,
            ImdbId = imdbId,
            MediaType = string.Equals(tmdbType, "tv", StringComparison.OrdinalIgnoreCase)
                ? MediaTypes.Series
                : MediaTypes.Movie,
            Category = string.Equals(tmdbType, "tv", StringComparison.OrdinalIgnoreCase)
                ? TitleCategories.Series
                : TitleCategories.Movie,
            OriginalTitle = "Pending",
            MetadataStatus = MetadataStatuses.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Titles.Add(title);
        await db.SaveChangesAsync();
        db.Availabilities.Add(new Availability
        {
            TitleId = title.Id,
            CatalogSourceId = source.Id,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return title.Id;
    }

    private async Task BuildServicesAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var dataPath = Path.Combine(Path.GetTempPath(), "PickArr-enrich-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataPath);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PickArr:DataPath"] = dataPath,
                ["PickArr:Tmdb:BaseUrl"] = _server.Urls[0] + "/3/",
                ["PickArr:Tmdb:RequestsPerSecond"] = "1000",
                ["PickArr:Tmdb:DisableResilience"] = "true",
                ["PickArr:Hangfire:Storage"] = "Memory",
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
        services.AddScoped<IEnrichmentService, EnrichmentService>();
        services.AddScoped<ITmdbImageUrlBuilder, TmdbImageUrlBuilder>();
        services.AddPickArrTmdb(config);
        _services = services.BuildServiceProvider();

        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        await db.Database.EnsureCreatedAsync();
        var store = scope.ServiceProvider.GetRequiredService<ISettingsStore>();
        await store.SetAsync(SettingsKeys.TmdbApiKey, JsonSerializer.Serialize("test-tmdb-api-key"), true);
    }

    private static string FindFixtureRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "fixtures", "tmdb");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.Combine(dir.FullName, "fixtures", "tmdb");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate tests/fixtures/tmdb");
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
    }
}
