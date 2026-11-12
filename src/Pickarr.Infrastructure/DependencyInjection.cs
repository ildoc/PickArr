using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Pickarr.Core.Abstractions;
using Pickarr.Infrastructure.Admin;
using Pickarr.Infrastructure.Arr;
using Pickarr.Infrastructure.Catalog;
using Pickarr.Infrastructure.Catalog.Watchmode;
using Pickarr.Infrastructure.Jobs;
using Pickarr.Infrastructure.Metadata;
using Pickarr.Infrastructure.Metadata.Reco;
using Pickarr.Infrastructure.Metadata.Tmdb;
using Pickarr.Infrastructure.Persistence;
using Pickarr.Infrastructure.Profiles;
using Pickarr.Infrastructure.Settings;
using Pickarr.Infrastructure.Diagnostics;
using Pickarr.Infrastructure.Time;
using Polly;

namespace Pickarr.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPickarrInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var configuredPath = configuration["Pickarr:DataPath"];
        var dataPath = string.IsNullOrWhiteSpace(configuredPath) ? "/config" : configuredPath;
        Directory.CreateDirectory(dataPath);

        var dbPath = Path.Combine(dataPath, "Pickarr.db");
        var keysPath = Path.Combine(dataPath, "keys");
        Directory.CreateDirectory(keysPath);

        var modelsPath = configuration["Pickarr:Models:Path"];
        var hangfireFile = configuration.GetSection(HangfireOptions.SectionName).Get<HangfireOptions>()?.SqliteFileName
            ?? new HangfireOptions().SqliteFileName;
        services.AddSingleton(new PickarrDataPaths(
            dataPath,
            jobsDatabaseFile: Path.Combine(dataPath, hangfireFile),
            modelsDirectory: string.IsNullOrWhiteSpace(modelsPath) ? null : modelsPath));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IApiUsageSyncRunAccessor, ApiUsageSyncRunAccessor>();
        services.AddDbContext<PickarrDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetApplicationName("Pickarr");

        services.AddSingleton<EnvironmentSettingSource>();
        services.AddScoped<ProtectedSettingsStore>();
        services.AddScoped<ISettingsStore>(sp => new EnvironmentSettingsStore(
            sp.GetRequiredService<ProtectedSettingsStore>(),
            sp.GetRequiredService<EnvironmentSettingSource>()));
        services.AddHttpClient(nameof(ConnectionProbeService));
        services.AddScoped<ConnectionProbeService>();
        services.AddScoped<SetupService>();
        services.AddScoped<ISyncRunStore, EfSyncRunStore>();
        services.AddScoped<IQuotaUsageReader, WatchmodeQuotaUsageReader>();
        services.AddScoped<ICatalogUpserter, CatalogUpserter>();
        services.AddScoped<TitleIdentityMaintenance>();
        services.AddScoped<ICatalogIncrementalSync, CatalogIncrementalSync>();
        services.AddScoped<ICatalogFullSync, CatalogFullSync>();
        services.AddScoped<ILeavingSync, LeavingSync>();
        services.AddScoped<ITitleQueryService, TitleQueryService>();
        services.AddScoped<CatalogConfigurationService>();
        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IWatchedService, WatchedService>();
        services.AddScoped<IRatingService, RatingService>();
        services.AddScoped<IRecoBuildService, RecoBuildService>();
        services.AddScoped<IRecoQueryService, RecoQueryService>();
        services.AddScoped<IRecoEvalService, RecoEvalService>();
        services.AddScoped<IRecoEventLogService, RecoEventLogService>();
        services.AddSingleton<IRecoLevel3Model, RecoLevel3Model>();
        services.AddSingleton<IRecoRebuildScheduler, RecoRebuildScheduler>();
        services.AddSingleton<RecoOnnxEmbedder>();
        services.AddScoped<IRecoModelService, RecoModelService>();
        services.AddScoped<IRecoEmbeddingService, RecoEmbeddingService>();
        services.AddHttpClient("reco-model", c => c.Timeout = TimeSpan.FromMinutes(10));
        services.AddScoped<IArrLibrarySync, ArrLibrarySync>();
        services.AddScoped<IArrAddService, ArrAddService>();
        services.AddScoped<IArrDeleteService, ArrDeleteService>();
        services.AddScoped<IArrClientFactory, ArrClientFactory>();
        services.Configure<CatalogBootstrapOptions>(configuration.GetSection(CatalogBootstrapOptions.SectionName));
        services.AddScoped<CatalogBootstrap>();
        services.AddScoped<IAdminJobService, AdminJobService>();
        services.AddScoped<IAdminQuotaService, AdminQuotaService>();
        services.AddScoped<ISystemStatsService, SystemStatsService>();
        services.AddScoped<ILogFileService, LogFileService>();
        services.AddMemoryCache();
        services.Configure<GhcrOptions>(configuration.GetSection(GhcrOptions.SectionName));
        services.AddHttpClient<IImageTagSource, GhcrImageTagClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GhcrOptions>>().Value;
                var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/";
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = options.RequestTimeout;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Pickarr/0.1");
            });
        services.AddScoped<IImageUpdateService, ImageUpdateService>();
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
        services.AddTransient<RecoBuildJob>();
        services.AddTransient<RecoEvalJob>();
        services.AddTransient<RecoFeatureJob>();
        services.AddTransient<RecoEmbeddingJob>();
        services.AddTransient<RecoModelDownloadJob>();
        services.AddScoped<ScheduleReconciler>();
        services.AddSingleton<ScheduleReconcilerStub>();
        services.AddScoped<IEnrichmentService, EnrichmentService>();
        services.AddScoped<ISeriesGuide, SeriesGuideService>();
        services.AddScoped<ITmdbImageUrlBuilder, TmdbImageUrlBuilder>();
        services.AddPickarrHangfire(configuration, dataPath);
        // After Hangfire so workers exist before the initial import is queued.
        services.AddHostedService<CatalogBootstrapHostedService>();
        services.AddHealthChecks()
            .AddDbContextCheck<PickarrDbContext>("database");

        services.AddPickarrWatchmode(configuration);
        services.AddPickarrTmdb(configuration);

        return services;
    }

    public static IServiceCollection AddPickarrTmdb(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<TmdbOptions>(configuration.GetSection(TmdbOptions.SectionName));
        services.AddSingleton<TmdbRateGate>();
        services.AddTransient<TmdbAuthHandler>();
        services.AddTransient<TmdbApiUsageHandler>();
        services.AddTransient<TmdbRateLimitHandler>();

        var disableResilience = configuration.GetValue($"{TmdbOptions.SectionName}:DisableResilience", false);
        var http = services.AddHttpClient<IMetadataProvider, TmdbMetadataProvider>((sp, client) =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TmdbOptions>>().Value;
                var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/";
                client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Pickarr/0.1");
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
                // Serialize with TmdbRateGate — avoid a concurrent burst ahead of the gate.
                o.RateLimiter.DefaultRateLimiterOptions.PermitLimit = 1;
                o.RateLimiter.DefaultRateLimiterOptions.QueueLimit = 64;
            });
        }

        return services;
    }

    public static IServiceCollection AddPickarrWatchmode(
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
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Pickarr/0.1");
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
