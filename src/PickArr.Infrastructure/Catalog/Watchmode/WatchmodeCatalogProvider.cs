using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PickArr.Core.Abstractions;
using PickArr.Core.Catalog;
using PickArr.Core.Settings;

namespace PickArr.Infrastructure.Catalog.Watchmode;

public sealed class WatchmodeCatalogProvider(
    HttpClient httpClient,
    ISettingsStore settingsStore,
    IOptions<WatchmodeOptions> options,
    ILogger<WatchmodeCatalogProvider> logger) : ICatalogProvider
{
    public const string ProviderType = "watchmode";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyList<CatalogEntryType> DefaultTypes =
    [
        CatalogEntryType.Movie,
        CatalogEntryType.TvSeries,
        CatalogEntryType.TvMiniseries,
        CatalogEntryType.TvSpecial,
    ];

    public string Type => ProviderType;

    public CatalogProviderCapabilities Capabilities
    {
        get
        {
            var paid = IsPaidPlanCached;
            return new CatalogProviderCapabilities(
                SupportsChangeFeed: paid,
                SupportsLeavingDates: true,
                MaxPageSize: options.Value.MaxPageSize);
        }
    }

    // Best-effort; refreshed on each call that needs the plan.
    private bool IsPaidPlanCached { get; set; }

    public async Task<IReadOnlyList<ProviderInfo>> GetAvailableProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        var sources = await GetAsync<List<WatchmodeSourceItem>>("sources/", cancellationToken);
        return sources
            .Select(s => new ProviderInfo(
                ExternalId: s.Id.ToString(),
                Name: s.Name,
                Type: s.Type,
                Regions: s.Regions))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetCountriesAsync(
        string providerExternalId,
        CancellationToken cancellationToken = default)
    {
        var providers = await GetAvailableProvidersAsync(cancellationToken);
        var match = providers.FirstOrDefault(p =>
            string.Equals(p.ExternalId, providerExternalId, StringComparison.Ordinal));
        if (match is not null && match.Regions.Count > 0)
        {
            return match.Regions;
        }

        var regions = await GetAsync<List<WatchmodeRegionItem>>("regions/", cancellationToken);
        return regions.Where(r => r.Supported).Select(r => r.Country).ToList();
    }

    public async Task<CatalogPage> ListTitlesAsync(
        CatalogListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Page must be >= 1.");
        }

        var max = options.Value.MaxPageSize;
        var pageSize = request.PageSize is < 1 or > 250 ? max : Math.Min(request.PageSize, max);
        var types = request.Types.Count == 0 ? DefaultTypes : request.Types;
        var typesParam = string.Join(',', types.Select(WatchmodeMapping.ToWatchmodeType));

        var path = new StringBuilder("list-titles/")
            .Append("?source_ids=").Append(Uri.EscapeDataString(request.ProviderExternalId))
            .Append("&regions=").Append(Uri.EscapeDataString(request.CountryCode))
            .Append("&types=").Append(Uri.EscapeDataString(typesParam))
            .Append("&sort_by=").Append(Uri.EscapeDataString(WatchmodeMapping.ToWatchmodeSort(request.Sort)))
            .Append("&page=").Append(request.Page)
            .Append("&limit=").Append(pageSize)
            .ToString();

        using var response = await SendAsync(path, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<WatchmodeListTitlesResponse>(JsonOptions, cancellationToken)
            ?? new WatchmodeListTitlesResponse();

        var credits = WatchmodeApiUsageHandler.ResolveCredits(response, "/list-titles/");

        return new CatalogPage(
            Entries: body.Titles.Select(WatchmodeMapping.ToCatalogEntry).ToList(),
            Page: body.Page == 0 ? request.Page : body.Page,
            TotalPages: body.TotalPages,
            CreditsUsed: credits,
            TotalResults: body.TotalResults);
    }

    public async Task<IReadOnlyList<LeavingEntry>> GetLeavingAsync(
        CatalogSourceRef source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var path =
            $"title-leaving-dates/?regions={Uri.EscapeDataString(source.CountryCode)}&limit=10";
        using var response = await SendAsync(path, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<WatchmodeLeavingResponse>(JsonOptions, cancellationToken)
            ?? new WatchmodeLeavingResponse();

        return body.LeavingDates
            .Where(x =>
                x.ProviderId is null ||
                string.Equals(x.ProviderId.ToString(), source.ProviderExternalId, StringComparison.Ordinal))
            .Select(WatchmodeMapping.ToLeavingEntry)
            .ToList();
    }

    public async Task<IReadOnlyList<CatalogEntry>> GetChangedTitlesAsync(
        CatalogSourceRef source,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        await RefreshPlanAsync(cancellationToken);
        if (!IsPaidPlanCached)
        {
            throw new NotSupportedException(
                "Watchmode change feed requires WatchmodePlan=paid (FR-CAT-13).");
        }

        var path =
            $"changes/titles_sources_changed/?regions={Uri.EscapeDataString(source.CountryCode)}" +
            $"&start_date={from:yyyy-MM-dd}&end_date={to:yyyy-MM-dd}" +
            $"&source_ids={Uri.EscapeDataString(source.ProviderExternalId)}";

        using var response = await SendAsync(path, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<WatchmodeChangesResponse>(JsonOptions, cancellationToken)
            ?? new WatchmodeChangesResponse();
        var items = body.Titles ?? body.Changes ?? [];
        return items.Select(WatchmodeMapping.ToCatalogEntry).ToList();
    }

    private async Task<T> GetAsync<T>(string relativePath, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(relativePath, cancellationToken);
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return value ?? throw new InvalidOperationException($"Empty Watchmode response for '{relativePath}'.");
    }

    private async Task<HttpResponseMessage> SendAsync(string relativePath, CancellationToken cancellationToken)
    {
        await RefreshPlanAsync(cancellationToken);
        var response = await httpClient.GetAsync(relativePath, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
        WatchmodeErrorBody? error = null;
        try
        {
            error = JsonSerializer.Deserialize<WatchmodeErrorBody>(errorText, JsonOptions);
        }
        catch (JsonException)
        {
            // ignore parse failures
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests ||
            string.Equals(error?.Code, "MONTHLY_QUOTA_EXCEEDED", StringComparison.OrdinalIgnoreCase))
        {
            response.Dispose();
            throw new WatchmodeQuotaExceededException(
                error?.StatusMessage ?? "Watchmode monthly quota exceeded.");
        }

        logger.LogWarning(
            "Watchmode call failed: {StatusCode} {Code} {Path}",
            (int)response.StatusCode,
            error?.Code,
            relativePath);

        response.Dispose();
        throw new HttpRequestException(
            $"Watchmode request failed ({(int)response.StatusCode}): {error?.Code ?? response.ReasonPhrase}");
    }

    private async Task RefreshPlanAsync(CancellationToken cancellationToken)
    {
        var raw = await settingsStore.GetRawAsync(SettingsKeys.WatchmodePlan, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            IsPaidPlanCached = false;
            return;
        }

        var plan = JsonSerializer.Deserialize<string>(raw, JsonOptions) ?? "free";
        IsPaidPlanCached = string.Equals(plan, "paid", StringComparison.OrdinalIgnoreCase);
    }
}
