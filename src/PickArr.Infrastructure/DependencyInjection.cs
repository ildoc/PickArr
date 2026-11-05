using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using PickArr.Core.Abstractions;
using PickArr.Infrastructure.Admin;
using PickArr.Infrastructure.Arr;
using PickArr.Infrastructure.Catalog;
using PickArr.Infrastructure.Catalog.Watchmode;
using PickArr.Infrastructure.Jobs;
using PickArr.Infrastructure.Metadata;
using PickArr.Infrastructure.Metadata.Tmdb;
using PickArr.Infrastructure.Persistence;
using PickArr.Infrastructure.Settings;
using PickArr.Infrastructure.Time;
using Polly;

namespace PickArr.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPickArrInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var configuredPath = configuration["PickArr:DataPath"];
        var dataPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PickArr")
            : configuredPath;
        Directory.CreateDirectory(dataPath);

        var dbPath = Path.Combine(dataPath, "PickArr.db");
        var keysPath = Path.Combine(dataPath, "keys");
        Directory.CreateDirectory(keysPath);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IApiUsageSyncRunAccessor, ApiUsageSyncRunAccessor>();
        services.AddDbContext<PickArrDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetApplicationName("PickArr");

        services.AddScoped<ISettingsStore, ProtectedSettingsStore>();
        services.AddScoped<ISyncRunStore, EfSyncRunStore>();
        services.AddScoped<IQuotaUsageReader, WatchmodeQuotaUsageReader>();
        services.AddScoped<ICatalogUpserter, CatalogUpserter>();
        services.AddScoped<ICatalogIncrementalSync, CatalogIncrementalSync>();
        services.AddScoped<ICatalogFullSync, CatalogFullSync>();
        services.AddScoped<ILeavingSync, LeavingSync>();
        services.AddScoped<ITitleQueryService, TitleQueryService>();
        services.AddScoped<CatalogConfigurationService>();
        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<IArrLibrarySync, ArrLibrarySync>();
        services.AddScoped<IArrAddService, ArrAddService>();
        services.AddScoped<IArrDeleteService, ArrDeleteService>();
        services.AddScoped<IArrClientFactory, ArrClientFactory>();
        services.Configure<CatalogBootstrapOptions>(configuration.GetSection(CatalogBootstrapOptions.SectionName));
        services.AddScoped<CatalogBootstrap>();
        services.AddScoped<IAdminJobService, AdminJobService>();
        services.AddScoped<IAdminQuotaService, AdminQuotaService>();
        services.AddHttpClient<SonarrClient>();
        services.AddHttpClient<RadarrClient>();
        services.AddScoped<DatabaseInitializer>();
        services.AddTransient<SpikeProbeJob>();
        services.AddTransient<CatalogIncrementalSyncJob>();
        services.AddTransient<CatalogFullSyncJob>();
        services.AddTransient<CatalogResumeJob>();
        services.AddTransient<CatalogBootstrapJob>();
        services.AddTransient<LeavingSyncJob>();
        services.AddTransient<EnrichmentJob>();
        services.AddTransient<MetadataRefreshJob>();
        services.AddTransient<ArrSyncJob>();
        services.AddScoped<ScheduleReconciler>();
        services.AddSingleton<ScheduleReconcilerStub>();
        services.AddScoped<IEnrichmentService, EnrichmentService>();
        services.AddScoped<ISeriesGuide, SeriesGuideService>();
        services.AddScoped<ITmdbImageUrlBuilder, TmdbImageUrlBuilder>();
        services.AddPickArrHangfire(configuration, dataPath);
        services.AddHealthChecks()
            .AddDbContextCheck<PickArrDbContext>("database");

        services.AddPickArrWatchmode(configuration);
        services.AddPickArrTmdb(configuration);

        return services;
    }

    public static IServiceCollection AddPickArrTmdb(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<TmdbOptions>(configuration.GetSection(TmdbOptions.SectionName));
        services.AddTransient<TmdbAuthHandler>();
        services.AddTransient<TmdbApiUsageHandler>();
        services.AddTransient<TmdbRateLimitHandler>();

        var disableResilience = configuration.GetValue($"{TmdbOptions.SectionName}:DisableResilience", false);
        var http = services.AddHttpClient<IMetadataProvider, TmdbMetadataProvider>((sp, client) =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TmdbOptions>>().Value;
                var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/";
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("PickArr/0.1");
            })
            .AddHttpMessageHandler<TmdbAuthHandler>()
            .AddHttpMessageHandler<TmdbApiUsageHandler>()
            .AddHttpMessageHandler<TmdbRateLimitHandler>();

        if (!disableResilience)
        {
            http.AddStandardResilienceHandler(o =>
            {
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
                o.Retry.MaxRetryAttempts = 3;
                o.Retry.BackoffType = DelayBackoffType.Exponential;
                o.Retry.UseJitter = true;
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                o.CircuitBreaker.MinimumThroughput = 5;
                o.RateLimiter.DefaultRateLimiterOptions.PermitLimit = 8;
                o.RateLimiter.DefaultRateLimiterOptions.QueueLimit = 32;
            });
        }

        return services;
    }

    public static IServiceCollection AddPickArrWatchmode(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<WatchmodeOptions>(configuration.GetSection(WatchmodeOptions.SectionName));

        services.AddTransient<WatchmodeAuthHandler>();
        services.AddTransient<WatchmodeApiUsageHandler>();
        services.AddTransient<WatchmodeRateLimitHandler>();

        var disableResilience = configuration.GetValue($"{WatchmodeOptions.SectionName}:DisableResilience", false);

        var http = services.AddHttpClient<ICatalogProvider, WatchmodeCatalogProvider>((sp, client) =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WatchmodeOptions>>().Value;
                var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/";
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("PickArr/0.1");
            })
            .AddHttpMessageHandler<WatchmodeAuthHandler>()
            .AddHttpMessageHandler<WatchmodeApiUsageHandler>()
            .AddHttpMessageHandler<WatchmodeRateLimitHandler>();

        if (!disableResilience)
        {
            http.AddStandardResilienceHandler(ConfigureWatchmodeResilience);
        }

        services.AddScoped<IProviderResolver, ProviderResolver>();
        return services;
    }

    private static void ConfigureWatchmodeResilience(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.Retry.ShouldHandle = args =>
        {
            // Do not retry quota / hard 429 — surface to credit budget.
            if (args.Outcome.Result?.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                return PredicateResult.False();
            }

            return HttpClientResiliencePredicates.IsTransient(args.Outcome)
                ? PredicateResult.True()
                : PredicateResult.False();
        };
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 5;
        options.RateLimiter.DefaultRateLimiterOptions.PermitLimit = 1;
        options.RateLimiter.DefaultRateLimiterOptions.QueueLimit = 8;
    }
}
