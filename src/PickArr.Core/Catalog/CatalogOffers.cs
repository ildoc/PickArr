namespace Pickarr.Core.Catalog;

public static class CatalogOffers
{
    public const int FreeMonthlyQuota = 2500;
    public const int FreeCountryLimit = 3;
    public const int CreditsPerCall = 1;
    public const int DefaultPagesPerCountry = 32;
    public const int FullSyncsPerMonth = 4;
    public const int DailyRunsPerMonth = 30;

    public static readonly IReadOnlyList<CatalogProviderOffer> Providers =
    [
        new("netflix", "Netflix", "203", true, ["US", "IT", "GB", "CA", "DE", "FR", "ES", "AU"]),
        new("prime-video", "Prime Video", "26", false, ["US", "IT", "GB", "CA", "DE", "FR", "ES", "AU"]),
        new("disney-plus", "Disney+", "372", false, ["US", "IT", "GB", "CA", "DE", "FR", "ES", "AU"]),
        new("hbo-max", "HBO Max", "387", false, ["US", "IT", "GB", "DE", "FR", "ES", "AU"]),
        new("hulu", "Hulu", "157", false, ["US"]),
        new("apple-tv-plus", "Apple TV+", "371", false, ["US", "IT", "GB", "CA", "DE", "FR", "ES", "AU"]),
        new("paramount-plus", "Paramount+", "444", false, ["US", "IT", "GB", "CA", "DE", "FR", "AU"]),
    ];

    public static IReadOnlyList<string> CountriesFor(string providerKey) =>
        Providers.FirstOrDefault(provider =>
            string.Equals(provider.Key, providerKey, StringComparison.OrdinalIgnoreCase))?.Countries
        ?? [];

    public static string Language(string countryCode) => countryCode.ToUpperInvariant() switch
    {
        "IT" => "it-IT",
        "DE" => "de-DE",
        "FR" => "fr-FR",
        "ES" => "es-ES",
        "GB" => "en-GB",
        "CA" => "en-CA",
        "AU" => "en-AU",
        _ => "en-US",
    };
}

public sealed record CatalogProviderOffer(
    string Key,
    string DisplayName,
    string ExternalId,
    bool EnabledByDefault,
    IReadOnlyList<string> Countries);

public sealed record CatalogCountryCost(
    bool Selected,
    int Pages,
    int IncrementalPages,
    bool FullyImported,
    string CountryCode);

public sealed record CatalogCreditEstimateResult(
    int Countries,
    int Catalogs,
    int FillCredits,
    int MonthlyCredits,
    bool OverQuota,
    bool OverCountryLimit);

public static class CatalogCreditEstimate
{
    public static CatalogCreditEstimateResult Calculate(
        IReadOnlyList<CatalogCountryCost> countries,
        int used,
        int quota,
        bool freePlan)
    {
        var selected = countries.Where(country => country.Selected).ToList();
        var distinctCountries = selected
            .Select(country => country.CountryCode.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .Count();
        var fill = selected
            .Where(country => !country.FullyImported)
            .Sum(country => country.Pages * CatalogOffers.CreditsPerCall);
        var monthly = selected.Sum(country =>
            (country.IncrementalPages * CatalogOffers.DailyRunsPerMonth
             + country.Pages * CatalogOffers.FullSyncsPerMonth
             + CatalogOffers.DailyRunsPerMonth) * CatalogOffers.CreditsPerCall);

        return new CatalogCreditEstimateResult(
            distinctCountries,
            selected.Count,
            fill,
            monthly,
            used + monthly > quota || used + fill > quota,
            freePlan && distinctCountries > CatalogOffers.FreeCountryLimit);
    }
}
