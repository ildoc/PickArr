using Pickarr.Core.Abstractions;
using Pickarr.Core.Admin;

namespace Pickarr.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin");

        group.MapGet("/jobs", async (IAdminJobService jobs, CancellationToken ct) =>
        {
            var list = await jobs.ListJobsAsync(ct);
            return Results.Ok(list);
        });

        group.MapPost("/jobs/{jobKey}/trigger", async (
            string jobKey,
            TriggerJobRequest? body,
            IAdminJobService jobs,
            CancellationToken ct) =>
        {
            try
            {
                var result = await jobs.TriggerAsync(jobKey, body ?? new TriggerJobRequest(null, null), ct);
                return Results.Accepted(
                    result.RunId is { } id ? $"/api/v1/admin/runs/{id}" : null,
                    new { runId = result.RunId, hangfireJobId = result.HangfireJobId });
            }
            catch (SyncAlreadyRunningException ex)
            {
                return Results.Json(
                    new { code = "SYNC_ALREADY_RUNNING", detail = ex.Message, kind = ex.Kind, sourceId = ex.SourceId },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { code = "INVALID_TRIGGER", detail = ex.Message });
            }
        });

        group.MapGet("/runs", async (
            string? kind,
            string? status,
            string? jobKey,
            int? page,
            int? pageSize,
            IAdminJobService jobs,
            CancellationToken ct) =>
        {
            var result = await jobs.ListRunsAsync(kind, status, page ?? 1, pageSize ?? 20, jobKey, ct);
            return Results.Ok(result);
        });

        group.MapGet("/runs/{runId:guid}", async (
            Guid runId,
            IAdminJobService jobs,
            CancellationToken ct) =>
        {
            var run = await jobs.GetRunAsync(runId, ct);
            return run is null ? Results.NotFound() : Results.Ok(run);
        });

        group.MapGet("/quota", async (IAdminQuotaService quota, CancellationToken ct) =>
        {
            var dto = await quota.GetWatchmodeQuotaAsync(ct);
            return Results.Ok(dto);
        });

        return endpoints;
    }
}
