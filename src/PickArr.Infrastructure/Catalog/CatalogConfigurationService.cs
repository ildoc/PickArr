using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Catalog;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Jobs;
using PickArr.Infrastructure.Persistence;

namespace PickArr.Infrastructure.Catalog;

public sealed class CatalogSetupException(string message) : Exception(message);

public sealed record CatalogSetupDto(
    int Quota,
    int Used,
    string Plan,
    int FreeCountryLimit,
    int CreditsPerCall,
    int FullSyncsPerMonth,
    int DailyRunsPerMonth,
    int DefaultPages,
    IReadOnlyList<CatalogProviderSetupDto> Providers);

public sealed record CatalogProviderSetupDto(
    int Id,
    string Key,
    string Name,
    bool Enabled,
    IReadOnlyList<CatalogCountrySetupDto> Countries);

public sealed record CatalogCountrySetupDto(
    string Code,
    bool Enabled,
    int Pages,
    bool PagesMeasured,
    int IncrementalPages,
    bool FullyImported);

public sealed record UpdateCatalogSetupRequest(IReadOnlyList<UpdateCatalogProviderRequest> Providers);

public sealed record UpdateCatalogProviderRequest(int Id, bool Enabled, IReadOnlyList<string> Countries);

public sealed class CatalogConfigurationService(
    PickArrDbContext db,
    ICreditBudget budget,
    ISettingsStore settings,
    ScheduleReconciler reconciler,
    CatalogBootstrap bootstrap)
{
    public async Task<CatalogSetupDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await budget.GetSnapshotAsync(cancellationToken);
        var plan = await ReadPlanAsync(cancellationToken);
        var providers = await db.Providers.AsNoTracking()
            .Include(provider => provider.CatalogSources)
            .OrderBy(provider => provider.DisplayName)
            .ToListAsync(cancellationToken);

        return new CatalogSetupDto(
            snapshot.Quota,
            snapshot.Used,
            plan,
            CatalogOffers.FreeCountryLimit,
            CatalogOffers.CreditsPerCall,
            CatalogOffers.FullSyncsPerMonth,
            CatalogOffers.DailyRunsPerMonth,
            CatalogOffers.DefaultPagesPerCountry,
            providers.Select(MapProvider).ToList());
    }

    public async Task<CatalogSetupDto> UpdateAsync(
        UpdateCatalogSetupRequest request,
        CancellationToken cancellationToken = default)
    {
        var providers = await db.Providers
            .Include(provider => provider.CatalogSources)
            .ToListAsync(cancellationToken);
        var byId = providers.ToDictionary(provider => provider.Id);

        var selected = new List<CatalogCountryCost>();
        foreach (var update in request.Providers)
        {
            if (!byId.TryGetValue(update.Id, out var provider))
            {
                throw new CatalogSetupException($"Unknown provider {update.Id}.");
            }

            var offered = CatalogOffers.CountriesFor(provider.Key);
            var countries = update.Countries
                .Select(code => code.Trim().ToUpperInvariant())
                .Where(code => code.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var unknown = countries.FirstOrDefault(code => !offered.Contains(code, StringComparer.OrdinalIgnoreCase));
            if (unknown is not null)
            {
                throw new CatalogSetupException($"{provider.DisplayName} does not offer {unknown}.");
            }

            if (update.Enabled && countries.Count == 0)
            {
                throw new CatalogSetupException($"{provider.DisplayName} needs at least one country.");
            }

            provider.IsEnabled = update.Enabled;
            foreach (var code in offered)
            {
                var source = provider.CatalogSources.FirstOrDefault(item =>
                    string.Equals(item.CountryCode, code, StringComparison.OrdinalIgnoreCase));
                if (source is null)
                {
                    source = new CatalogSource
                    {
                        ProviderId = provider.Id,
                        CountryCode = code,
                        MetadataLanguage = CatalogOffers.Language(code),
                    };
                    provider.CatalogSources.Add(source);
                    db.CatalogSources.Add(source);
                }

                source.IsEnabled = countries.Contains(code, StringComparer.OrdinalIgnoreCase);
                if (update.Enabled && source.IsEnabled)
                {
                    selected.Add(new CatalogCountryCost(
                        true,
                        source.LastFullTotalPages is > 0 ? source.LastFullTotalPages.Value : CatalogOffers.DefaultPagesPerCountry,
                        Math.Max(1, source.IncrementalPages),
                        source.LastFullAt is not null,
                        code));
                }
            }
        }

        var snapshot = await budget.GetSnapshotAsync(cancellationToken);
        var freePlan = await ReadPlanAsync(cancellationToken) != "paid";
        var estimate = CatalogCreditEstimate.Calculate(selected, snapshot.Used, snapshot.Quota, freePlan);
        if (estimate.OverCountryLimit)
        {
            throw new CatalogSetupException(
                $"The free Watchmode plan allows {CatalogOffers.FreeCountryLimit} countries. {estimate.Countries} are selected.");
        }

        if (estimate.OverQuota)
        {
            throw new CatalogSetupException(
                $"This selection needs about {estimate.MonthlyCredits} Watchmode credits a month, and {estimate.FillCredits} to fill new catalogs. {snapshot.Used} of {snapshot.Quota} are already used.");
        }

        await db.SaveChangesAsync(cancellationToken);
        await reconciler.ReconcileAsync(cancellationToken);
        await bootstrap.StartAsync(force: false, cancellationToken);
        return await GetAsync(cancellationToken);
    }

    private static CatalogProviderSetupDto MapProvider(Provider provider)
    {
        var existing = provider.CatalogSources.ToDictionary(
            source => source.CountryCode,
            StringComparer.OrdinalIgnoreCase);
        var countries = CatalogOffers.CountriesFor(provider.Key)
            .Select(code =>
            {
                existing.TryGetValue(code, out var source);
                var measured = source?.LastFullTotalPages is > 0;
                return new CatalogCountrySetupDto(
                    code,
                    source?.IsEnabled == true,
                    measured ? source!.LastFullTotalPages!.Value : CatalogOffers.DefaultPagesPerCountry,
                    measured,
                    Math.Max(1, source?.IncrementalPages ?? 3),
                    source?.LastFullAt is not null);
            })
            .ToList();
        return new CatalogProviderSetupDto(
            provider.Id,
            provider.Key,
            provider.DisplayName,
            provider.IsEnabled,
            countries);
    }

    private async Task<string> ReadPlanAsync(CancellationToken cancellationToken)
    {
        var raw = await settings.GetRawAsync(SettingsKeys.WatchmodePlan, cancellationToken);
        var plan = raw?.Trim().Trim('"').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(plan) ? "free" : plan;
    }
}
