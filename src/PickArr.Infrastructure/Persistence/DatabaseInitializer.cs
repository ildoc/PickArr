using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PickArr.Core.Abstractions;
using PickArr.Core.Catalog;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Settings;

namespace PickArr.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    PickArrDbContext db,
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

        await EnsureDefaultSettingsAsync(now, cancellationToken);
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
