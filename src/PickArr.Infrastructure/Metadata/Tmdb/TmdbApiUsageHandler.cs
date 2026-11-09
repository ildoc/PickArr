using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain.Entities;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Metadata.Tmdb;

public sealed class TmdbApiUsageHandler(
    IServiceScopeFactory scopeFactory,
    IApiUsageSyncRunAccessor syncRunAccessor,
    IClock clock,
    ILogger<TmdbApiUsageHandler> logger) : DelegatingHandler
{
    public const string ServiceName = "tmdb";

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
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
            db.ApiUsages.Add(new ApiUsage
            {
                Id = Guid.NewGuid(),
                Service = ServiceName,
                Endpoint = NormalizeEndpoint(request.RequestUri),
                Timestamp = clock.UtcNow,
                Credits = 0,
                StatusCode = (int)response.StatusCode,
                SyncRunId = syncRunAccessor.CurrentSyncRunId,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist TMDB ApiUsage");
        }
    }

    internal static string NormalizeEndpoint(Uri? uri)
    {
        if (uri is null)
        {
            return "/";
        }

        var path = uri.AbsolutePath;
        if (string.IsNullOrEmpty(path))
        {
            path = "/";
        }

        // Collapse /movie/550 → /movie/{id}, /tv/1396 → /tv/{id}
        var parts = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && parts[0] is "movie" or "tv" or "find" && int.TryParse(parts[1], out _))
        {
            return $"/{parts[0]}/{{id}}";
        }

        if (parts.Length >= 2 && parts[0] == "find")
        {
            return "/find/{imdb_id}";
        }

        return path.StartsWith('/') ? path : "/" + path;
    }
}
