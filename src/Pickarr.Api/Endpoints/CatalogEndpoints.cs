using Pickarr.Core.Abstractions;
using Pickarr.Core.Catalog;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Api.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/catalog");

        group.MapGet("/sources", async (ITitleQueryService titles, CancellationToken ct) =>
            Results.Ok(await titles.ListSourcesAsync(ct)));

        group.MapGet("/types", async (ITitleQueryService titles, CancellationToken ct) =>
            Results.Ok(await titles.ListEnabledTypesAsync(ct)));

        group.MapGet("/genres", async (ITitleQueryService titles, CancellationToken ct) =>
            Results.Ok(await titles.ListEnabledGenresAsync(ct)));

        group.MapGet("/sources/{sourceId:int}/titles", async (
            int sourceId,
            HttpContext http,
            ITitleQueryService titles,
            PickarrDbContext db,
            string? q,
            string? view,
            string? type,
            string? genre,
            int? yearFrom,
            int? yearTo,
            string? arrState,
            bool? includeRemoved,
            double? minRating,
            string? sort,
            string? dir,
            int? page,
            int? pageSize,
            CancellationToken ct) =>
        {
            var (profileId, error) = await ProfileRequest.ReadOptionalPersonalAsync(http, db, ct);
            if (error is not null)
            {
                return error;
            }

            var request = new TitleQueryRequest(
                Q: q,
                View: view ?? "all",
                Types: SplitCsv(type),
                Genres: ParseIntCsv(genre),
                YearFrom: yearFrom,
                YearTo: yearTo,
                ArrStates: SplitCsv(arrState),
                Favorite: null,
                IncludeRemoved: includeRemoved == true,
                MinRating: minRating,
                Sort: sort ?? "added",
                Dir: dir ?? "desc",
                Page: page ?? 1,
                PageSize: pageSize ?? 48);

            return Results.Ok(await titles.QueryTitlesAsync(sourceId, request, ct, profileId));
        });

        group.MapGet("/sources/{sourceId:int}/genres", async (
            int sourceId,
            ITitleQueryService titles,
            CancellationToken ct) =>
            Results.Ok(await titles.ListGenresAsync(sourceId, favoritesOnly: false, ct)));

        group.MapGet("/sources/{sourceId:int}/types", async (
            int sourceId,
            ITitleQueryService titles,
            CancellationToken ct) =>
            Results.Ok(await titles.ListTypesAsync(sourceId, favoritesOnly: false, ct)));

        return endpoints;
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
