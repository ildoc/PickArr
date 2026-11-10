using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Arr;
using Pickarr.Core.Domain;
using Pickarr.Core.Recommendations;
using Pickarr.Infrastructure.Catalog;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Api.Endpoints;

public static class TitlesEndpoints
{
    public static IEndpointRouteBuilder MapTitlesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/api/v1/titles/{id:int}/rating", async (
            int id,
            SetRatingRequest body,
            HttpContext http,
            IRatingService ratings,
            PickarrDbContext db,
            CancellationToken ct) =>
        {
            var (profileId, error) = await ProfileRequest.RequirePersonalAsync(http, db, ct);
            if (error is not null)
            {
                return error;
            }

            if (body?.Value is null || !RatingValues.IsValid(body.Value))
            {
                return Results.BadRequest(new { code = "INVALID_RATING", detail = "value must be love, like, dislike, or not_interested." });
            }

            try
            {
                await ratings.SetAsync(profileId, id, RatingValues.Normalize(body.Value), RatingEventOrigins.Ui, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (RecoDisabledException)
            {
                return Results.Json(new { code = "RECO_DISABLED", detail = "Recommendations feature is disabled." }, statusCode: StatusCodes.Status409Conflict);
            }
        });

        endpoints.MapDelete("/api/v1/titles/{id:int}/rating", async (
            int id,
            HttpContext http,
            IRatingService ratings,
            PickarrDbContext db,
            CancellationToken ct) =>
        {
            var (profileId, error) = await ProfileRequest.RequirePersonalAsync(http, db, ct);
            if (error is not null)
            {
                return error;
            }

            try
            {
                await ratings.ClearAsync(profileId, id, RatingEventOrigins.Ui, ct);
                return Results.NoContent();
            }
            catch (RecoDisabledException)
            {
                return Results.Json(new { code = "RECO_DISABLED", detail = "Recommendations feature is disabled." }, statusCode: StatusCodes.Status409Conflict);
            }
        });

        endpoints.MapPut("/api/v1/titles/{id:int}/watched", async (
            int id,
            HttpContext http,
            IWatchedService watched,
            PickarrDbContext db,
            CancellationToken ct) =>
        {
            var (profileId, error) = await ProfileRequest.RequirePersonalAsync(http, db, ct);
            if (error is not null)
            {
                return error;
            }

            try
            {
                await watched.AddAsync(profileId, id, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        endpoints.MapDelete("/api/v1/titles/{id:int}/watched", async (
            int id,
            HttpContext http,
            IWatchedService watched,
            PickarrDbContext db,
            CancellationToken ct) =>
        {
            var (profileId, error) = await ProfileRequest.RequirePersonalAsync(http, db, ct);
            if (error is not null)
            {
                return error;
            }

            await watched.RemoveAsync(profileId, id, ct);
            return Results.NoContent();
        });

        endpoints.MapGet("/api/v1/titles/{id:int}", async (
            int id,
            int? sourceId,
            HttpContext http,
            ITitleQueryService titles,
            PickarrDbContext db,
            CancellationToken ct) =>
        {
            var (profileId, error) = await ProfileRequest.ReadOptionalPersonalAsync(http, db, ct);
            if (error is not null)
            {
                return error;
            }

            var detail = await titles.GetTitleAsync(id, sourceId, ct, profileId);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });

        endpoints.MapGet("/api/v1/titles/{id:int}/similar", async (
            int id,
            int? sourceId,
            int? limit,
            HttpContext http,
            IRecoQueryService reco,
            PickarrDbContext db,
            CancellationToken ct) =>
        {
            var (profileId, error) = await ProfileRequest.ReadOptionalPersonalAsync(http, db, ct);
            if (error is not null)
            {
                return error;
            }

            try
            {
                var result = await reco.QuerySimilarAsync(
                    new SimilarTitlesQuery(id, sourceId, limit ?? 20, profileId),
                    ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (RecoDisabledException)
            {
                return Results.Json(
                    new { code = "RECO_DISABLED", detail = "Recommendations feature is disabled." },
                    statusCode: StatusCodes.Status409Conflict);
            }
        });

        endpoints.MapGet("/api/v1/titles/{id:int}/seasons", async (
            int id,
            int? sourceId,
            ISeriesGuide guide,
            CancellationToken ct) =>
        {
            var seasons = await guide.ListSeasonsAsync(id, sourceId, ct);
            return seasons is null ? Results.NotFound() : Results.Ok(seasons);
        });

        endpoints.MapGet("/api/v1/titles/{id:int}/seasons/{seasonNumber:int}/episodes", async (
            int id,
            int seasonNumber,
            int? sourceId,
            ISeriesGuide guide,
            CancellationToken ct) =>
        {
            var episodes = await guide.ListEpisodesAsync(id, seasonNumber, sourceId, ct);
            return episodes is null ? Results.NotFound() : Results.Ok(episodes);
        });

        endpoints.MapPost("/api/v1/titles/{id:int}/arr", async (
            int id,
            HttpRequest request,
            HttpContext http,
            IArrAddService add,
            PickarrDbContext db,
            CancellationToken ct) =>
        {
            JsonElement body;
            try
            {
                body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body, cancellationToken: ct);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { code = "INVALID_BODY", detail = "Invalid JSON body." });
            }

            try
            {
                var profileId = await ProfileRequest.ReadAttributedIdAsync(http, db, ct);
                var arr = LooksLikeSonarr(body)
                    ? await add.AddAsync(id, ParseSonarrOptions(body), ct, profileId)
                    : await add.AddAsync(id, ParseRadarrOptions(body), ct, profileId);
                return Results.Created($"/api/v1/titles/{id}", arr);
            }
            catch (ArrOperationException ex)
            {
                return ArrProblem(ex);
            }
        });

        endpoints.MapDelete("/api/v1/titles/{id:int}/arr", async (
            int id,
            bool? deleteFiles,
            bool? addImportExclusion,
            HttpContext http,
            IArrDeleteService delete,
            PickarrDbContext db,
            CancellationToken ct) =>
        {
            if (deleteFiles is null || addImportExclusion is null)
            {
                return Results.BadRequest(new
                {
                    code = "MISSING_PARAMS",
                    detail = "Query parameters deleteFiles and addImportExclusion are required.",
                });
            }

            try
            {
                var profileId = await ProfileRequest.ReadAttributedIdAsync(http, db, ct);
                var arr = await delete.DeleteAsync(id, deleteFiles.Value, addImportExclusion.Value, ct, profileId);
                return Results.Ok(arr);
            }
            catch (ArrOperationException ex)
            {
                return ArrProblem(ex);
            }
        });

        endpoints.MapPost("/api/v1/titles/{id:int}/arr/refresh", async (
            int id,
            IArrClientFactory factory,
            IArrLibrarySync sync,
            PickarrDbContext db,
            ITitleQueryService titles,
            CancellationToken ct) =>
        {
            var title = await db.Titles.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
            if (title is null)
            {
                return Results.NotFound();
            }

            var kind = title.MediaType == MediaTypes.Series ? ArrKind.Sonarr : ArrKind.Radarr;
            var client = await factory.TryCreateAsync(kind, ct);
            if (client is null)
            {
                return Results.Json(new { code = "ARR_NOT_CONFIGURED" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            // Refresh whole library cache for the kind (MVP); item-level refresh arrives with webhooks.
            await sync.SyncAsync(kind, ct);
            var detail = await titles.GetTitleAsync(id, sourceId: null, ct);
            return Results.Ok(detail?.Arr);
        });

        return endpoints;
    }

    private static bool LooksLikeSonarr(JsonElement body) =>
        body.TryGetProperty("monitor", out _) || body.TryGetProperty("seriesType", out _);

    private static SonarrAddOptions ParseSonarrOptions(JsonElement body) =>
        new(
            body.GetProperty("qualityProfileId").GetInt32(),
            body.GetProperty("rootFolderPath").GetString() ?? "",
            body.TryGetProperty("monitor", out var monitor) ? monitor.GetString() ?? "all" : "all",
            body.TryGetProperty("monitorNewItems", out var mni) ? mni.GetString() ?? "all" : "all",
            body.TryGetProperty("seriesType", out var st) ? st.GetString() ?? "standard" : "standard",
            !body.TryGetProperty("seasonFolder", out var sf) || sf.GetBoolean(),
            ReadTags(body),
            body.TryGetProperty("searchNow", out var sn) && sn.GetBoolean(),
            ReadSeasons(body));

    private static RadarrAddOptions ParseRadarrOptions(JsonElement body) =>
        new(
            body.GetProperty("qualityProfileId").GetInt32(),
            body.GetProperty("rootFolderPath").GetString() ?? "",
            !body.TryGetProperty("monitored", out var mon) || mon.GetBoolean(),
            body.TryGetProperty("minimumAvailability", out var ma) ? ma.GetString() ?? "released" : "released",
            ReadTags(body),
            body.TryGetProperty("searchNow", out var sn) && sn.GetBoolean());

    private static IReadOnlyList<int> ReadTags(JsonElement body)
    {
        if (!body.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return tags.EnumerateArray().Select(t => t.GetInt32()).ToList();
    }

    private static IReadOnlyList<int>? ReadSeasons(JsonElement body)
    {
        if (!body.TryGetProperty("seasons", out var seasons) || seasons.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var numbers = seasons.EnumerateArray()
            .Where(season => season.ValueKind == JsonValueKind.Number)
            .Select(season => season.GetInt32())
            .Distinct()
            .ToList();
        return numbers.Count == 0 ? null : numbers;
    }

    private static IResult ArrProblem(ArrOperationException ex) =>
        Results.Json(
            new { code = ex.Code, detail = ex.Message, errors = ex.Errors },
            statusCode: ex.StatusCode);
}

public sealed record SetRatingRequest(string? Value);
