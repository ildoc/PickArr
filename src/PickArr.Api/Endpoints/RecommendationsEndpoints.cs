using PickArr.Core.Abstractions;
using PickArr.Core.Recommendations;
using PickArr.Infrastructure.Catalog;

namespace PickArr.Api.Endpoints;

public static class RecommendationsEndpoints
{
    public static IEndpointRouteBuilder MapRecommendationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/recommendations");

        group.MapGet("", async (
            IRecoQueryService reco,
            string? scope,
            int? sourceId,
            string? type,
            string? genre,
            int? yearFrom,
            int? yearTo,
            bool? includeInLibrary,
            bool? diversify,
            int? page,
            int? pageSize,
            CancellationToken ct) =>
        {
            try
            {
                var query = new RecommendationsQuery(
                    Scope: scope ?? RecoMembership.ScopeSource,
                    SourceId: sourceId,
                    Types: SplitCsv(type),
                    Genres: ParseIntCsv(genre),
                    YearFrom: yearFrom,
                    YearTo: yearTo,
                    IncludeInLibrary: includeInLibrary ?? false,
                    Diversify: diversify ?? true,
                    Page: page ?? 1,
                    PageSize: pageSize ?? 48);
                return Results.Ok(await reco.QueryAsync(query, ct));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { code = "INVALID_SCOPE", detail = ex.Message });
            }
            catch (RecoDisabledException)
            {
                return Results.Json(
                    new { code = "RECO_DISABLED", detail = "Recommendations feature is disabled." },
                    statusCode: StatusCodes.Status409Conflict);
            }
        });

        // Status stays readable when reco is disabled so the UI can show the toggle state.
        group.MapGet("/status", async (IRecoQueryService reco, CancellationToken ct) =>
            Results.Ok(await reco.GetStatusAsync(ct)));

        group.MapGet("/onboarding", async (
            IRecoQueryService reco,
            int? sourceId,
            string? exclude,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await reco.GetOnboardingAsync(sourceId, ParseIntCsv(exclude), ct));
            }
            catch (RecoDisabledException)
            {
                return Results.Json(
                    new { code = "RECO_DISABLED", detail = "Recommendations feature is disabled." },
                    statusCode: StatusCodes.Status409Conflict);
            }
        });

        group.MapPost("/events", async (
            List<RecoEventLogEntryDto>? body,
            IRecoEventLogService eventLog,
            CancellationToken ct) =>
        {
            if (body is null || body.Count == 0)
            {
                return Results.NoContent();
            }

            await eventLog.LogManyAsync(
                body.Select(e => new RecoEventLogEntry(
                    e.TitleId,
                    e.Event,
                    e.Version,
                    e.Rank,
                    e.Scope)).ToList(),
                ct);
            return Results.NoContent();
        });

        return endpoints;
    }

    public sealed record RecoEventLogEntryDto(
        int TitleId,
        string Event,
        int? Version = null,
        int? Rank = null,
        string? Scope = null);

    private static IReadOnlyList<string>? SplitCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : parts;
    }

    private static IReadOnlyList<int>? ParseIntCsv(string? value)
    {
        var parts = SplitCsv(value);
        if (parts is null)
        {
            return null;
        }

        var ids = new List<int>();
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var id))
            {
                ids.Add(id);
            }
        }

        return ids.Count == 0 ? null : ids;
    }
}
