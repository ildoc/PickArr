using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Catalog;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Settings;

namespace Pickarr.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    PickarrDbContext db,
    IClock clock,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        // WAL + busy timeout: better concurrent Hangfire/API readers; note SP-5 for Docker volume caveats.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;", cancellationToken);
        await SeedAsync(cancellationToken);
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        foreach (var offer in CatalogOffers.Providers)
        {
            var provider = await EnsureProviderAsync(offer, now, cancellationToken);
            foreach (var country in offer.Countries)
            {
                await EnsureSourceAsync(provider, country, cancellationToken);
            }
        }

        await MigrateLegacySettingKeysAsync(now, cancellationToken);
        await EnsureDefaultSettingsAsync(now, cancellationToken);
    }

    private async Task MigrateLegacySettingKeysAsync(DateTime now, CancellationToken cancellationToken)
    {
        // tmdb.token → tmdb.apiKey (env TMDB_API_KEY). Keep the encrypted value.
        const string legacyTmdbToken = "tmdb.token";
        var legacy = await db.Settings.FirstOrDefaultAsync(s => s.Key == legacyTmdbToken, cancellationToken);
        if (legacy is null)
        {
            return;
        }

        var already = await db.Settings.AnyAsync(s => s.Key == SettingsKeys.TmdbApiKey, cancellationToken);
        if (!already)
        {
            db.Settings.Add(new AppSetting
            {
                Key = SettingsKeys.TmdbApiKey,
                ValueJson = legacy.ValueJson,
                IsSecret = legacy.IsSecret,
                UpdatedAt = now,
            });
        }

        db.Settings.Remove(legacy);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Migrated setting {From} to {To}", legacyTmdbToken, SettingsKeys.TmdbApiKey);
    }

    private async Task<Provider> EnsureProviderAsync(
        CatalogProviderOffer offer,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var provider = await db.Providers.FirstOrDefaultAsync(p => p.Key == offer.Key, cancellationToken);
        if (provider is not null)
        {
            return provider;
        }

        provider = new Provider
        {
            Key = offer.Key,
            DisplayName = offer.DisplayName,
            ProviderType = "watchmode",
            ExternalId = offer.ExternalId,
            IsEnabled = offer.EnabledByDefault,
            CreatedAt = now,
        };
        db.Providers.Add(provider);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded provider {Provider}", offer.DisplayName);
        return provider;
    }

    private async Task EnsureSourceAsync(Provider provider, string country, CancellationToken cancellationToken)
    {
        var exists = await db.CatalogSources.AnyAsync(
            s => s.ProviderId == provider.Id && s.CountryCode == country,
            cancellationToken);
        if (exists)
        {
            return;
        }

        db.CatalogSources.Add(new CatalogSource
        {
            ProviderId = provider.Id,
            CountryCode = country,
            MetadataLanguage = CatalogOffers.Language(country),
            IsEnabled = false,
        });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded catalog source {Provider} {Country}", provider.DisplayName, country);
    }

    private async Task EnsureDefaultSettingsAsync(DateTime now, CancellationToken cancellationToken)
    {
        await EnsureSettingAsync(SettingsKeys.WatchmodePlan, "\"free\"", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.WatchmodeQuota, "2500", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.WatchmodeReservePercent, "10", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.WatchmodeMaxCreditsPerRun, "80", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.WatchmodeBillingDayOfMonth, "1", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.CatalogMaxRemovalRatio, "0.1", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.CatalogRemovalConfirmations, "2", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.CatalogNewWindowDays, "14", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.CatalogLeavingWindowDays, "30", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.CatalogRemovedWindowDays, "30", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.CatalogStaleWarningDays, "21", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.UiLanguage, "\"en\"", false, now, cancellationToken);
        await EnsureSettingAsync("sonarr.syncIntervalMinutes", "15", false, now, cancellationToken);
        await EnsureSettingAsync("radarr.syncIntervalMinutes", "15", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoEnabled, "true", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoUseImplicit, "true", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoExcludeInLibrary, "true", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoN0, "30", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoWeightLove, "1.5", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoWeightLike, "1", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoWeightDislike, "-1", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoWeightNotInterested, "-0.75", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoWeightFavorite, "0.5", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoWeightArrAdd, "0.6", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoWeightDownloaded, "0.4", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoWeightWatched, "0", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoEmbeddingsEnabled, "true", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoEmbeddingsModelId, "\"all-MiniLM-L6-v2@q8\"", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoEmbeddingsThreads, "2", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoEmbeddingsBatchSize, "16", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoEmbeddingsMinCoverage, "0.8", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoEmbeddingsThrottle, "true", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoMmrLambda, "0.7", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoExploreRate, "0.1", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoMlMinRatings, "100", false, now, cancellationToken);
        await EnsureSettingAsync(SettingsKeys.RecoEventsEnabled, "false", false, now, cancellationToken);
    }

    private async Task EnsureSettingAsync(
        string key,
        string valueJson,
        bool isSecret,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var exists = await db.Settings.AnyAsync(s => s.Key == key, cancellationToken);
        if (exists)
        {
            return;
        }

        db.Settings.Add(new AppSetting
        {
            Key = key,
            ValueJson = valueJson,
            IsSecret = isSecret,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
