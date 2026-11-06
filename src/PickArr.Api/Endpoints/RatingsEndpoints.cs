using System.Text.Json;
using PickArr.Core.Abstractions;
using PickArr.Core.Catalog;
using PickArr.Core.Domain;
using PickArr.Core.Recommendations;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Catalog;

namespace PickArr.Api.Endpoints;

public static class RatingsEndpoints
{
    public static IEndpointRouteBuilder MapRatingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/ratings");

        group.MapGet("", async (
            ITitleQueryService titles,
            ISettingsStore settings,
            string? value,
            string? q,
            string? type,
            string? genre,
            string? sort,
            string? dir,
            int? page,
            int? pageSize,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(value) || !RatingValues.IsValid(value))
            {
                return Results.BadRequest(new { code = "INVALID_RATING", detail = "value must be like or dislike." });
            }

            if (await IsRecoDisabledAsync(settings, ct))
            {
                return Results.Json(
                    new { code = "RECO_DISABLED", detail = "Recommendations feature is disabled." },
                    statusCode: StatusCodes.Status409Conflict);
            }

            var request = new TitleQueryRequest(
                Q: q,
                Types: SplitCsv(type),
                Genres: ParseIntCsv(genre),
                Sort: sort ?? "added",
                Dir: dir ?? "desc",
                Page: page ?? 1,
                PageSize: pageSize ?? 48);
            return Results.Ok(await titles.QueryRatingsAsync(value, request, ct));
        });

        group.MapGet("/export", async (IRatingService ratings, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await ratings.ExportAsync(ct));
            }
            catch (RecoDisabledException)
            {
                return Results.Json(
                    new { code = "RECO_DISABLED", detail = "Recommendations feature is disabled." },
                    statusCode: StatusCodes.Status409Conflict);
            }
        });

        group.MapPost("/import", async (
            RatingImportRequest? body,
            IRatingService ratings,
            CancellationToken ct) =>
        {
            if (body is null || body.Items is null)
            {
                return Results.BadRequest(new { code = "INVALID_BODY", detail = "items are required." });
            }

            try
            {
                var result = await ratings.ImportAsync(body, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { code = "INVALID_MODE", detail = ex.Message });
            }
            catch (RecoDisabledException)
            {
                return Results.Json(
                    new { code = "RECO_DISABLED", detail = "Recommendations feature is disabled." },
                    statusCode: StatusCodes.Status409Conflict);
            }
        });

        group.MapPost("/onboarding", async (
            List<RatingOnboardingItem>? body,
            IRatingService ratings,
            CancellationToken ct) =>
        {
            if (body is null)
            {
                return Results.BadRequest(new { code = "INVALID_BODY", detail = "items are required." });
            }

            try
            {
                await ratings.OnboardingAsync(body, ct);
                return Results.NoContent();
            }
            catch (RecoDisabledException)
            {
                return Results.Json(
                    new { code = "RECO_DISABLED", detail = "Recommendations feature is disabled." },
                    statusCode: StatusCodes.Status409Conflict);
            }
        });

        return endpoints;
    }

    private static async Task<bool> IsRecoDisabledAsync(ISettingsStore settings, CancellationToken ct)
    {
        var raw = await settings.GetRawAsync(SettingsKeys.RecoEnabled, ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return !JsonSerializer.Deserialize<bool>(raw);
    }

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
