using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain.Entities;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Catalog.Watchmode;

public sealed class WatchmodeApiUsageHandler(
    IServiceScopeFactory scopeFactory,
    IApiUsageSyncRunAccessor syncRunAccessor,
    IClock clock,
    ILogger<WatchmodeApiUsageHandler> logger) : DelegatingHandler
{
    public const string ServiceName = "watchmode";
    public const string QuotaHeader = "X-Account-Quota";
    public const string QuotaUsedHeader = "X-Account-Quota-Used";
    public const string RequestCostHeader = "X-Watchmode-Request-Cost";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        await TryRecordAsync(request, response, cancellationToken);
        return response;
    }

    private async Task TryRecordAsync(
        HttpRequestMessage request,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = NormalizeEndpoint(request.RequestUri);
            var credits = ResolveCredits(response, endpoint);
            int? quotaUsed = TryParseIntHeader(response, QuotaUsedHeader);
            int? quotaLimit = TryParseIntHeader(response, QuotaHeader);

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
            db.ApiUsages.Add(new ApiUsage
            {
                Id = Guid.NewGuid(),
                Service = ServiceName,
                Endpoint = endpoint,
                Timestamp = clock.UtcNow,
                Credits = credits,
                QuotaUsed = quotaUsed,
                QuotaLimit = quotaLimit,
                StatusCode = (int)response.StatusCode,
                SyncRunId = syncRunAccessor.CurrentSyncRunId,
            });
            await db.SaveChangesAsync(cancellationToken);

            if (quotaUsed is not null || quotaLimit is not null)
            {
                var budget = scope.ServiceProvider.GetRequiredService<ICreditBudget>();
                await budget.ObserveQuotaAsync(quotaUsed, quotaLimit, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist Watchmode ApiUsage");
        }
    }

    internal static string NormalizeEndpoint(Uri? uri)
    {
        if (uri is null)
        {
            return "/";
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.StartsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            path = path[3..];
        }

        if (string.IsNullOrEmpty(path))
        {
            path = "/";
        }

        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        if (!path.EndsWith('/'))
        {
            path += "/";
        }

        return path;
    }

    internal static int ResolveCredits(HttpResponseMessage response, string endpoint)
    {
        if (TryParseIntHeader(response, RequestCostHeader) is { } cost)
        {
            return cost;
        }

        // Catalog classic responses usually omit cost header (SP-1).
        return endpoint switch
        {
            "/status/" => 0,
            "/sources/" => 1,
            "/regions/" => 1,
            "/list-titles/" => 1,
            "/title-leaving-dates/" => 1,
            _ when endpoint.StartsWith("/changes/", StringComparison.OrdinalIgnoreCase) => 1,
            _ => response.IsSuccessStatusCode ? 1 : 0,
        };
    }

    private static int? TryParseIntHeader(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues(name, out var values))
        {
            return null;
        }

        var raw = values.FirstOrDefault();
        return int.TryParse(raw, out var value) ? value : null;
    }
}
