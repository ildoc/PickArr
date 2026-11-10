using Pickarr.Core.Abstractions;
using Pickarr.Core.Catalog;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Api.Endpoints;

public static class FavoritesEndpoints
{
    public static IEndpointRouteBuilder MapFavoritesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/favorites");

        group.MapGet("", async (
            HttpContext http,
            PickarrDbContext db,
            ITitleQueryService titles,
            string? q,
            string? type,
            string? genre,
            string? sort,
            string? dir,
            int? page,
            int? pageSize,
            CancellationToken ct) =>
        {
            var (profileId, error) = await ProfileRequest.RequirePersonalAsync(http, db, ct);
            if (error is not null)
            {
                return error;
            }

            var request = new TitleQueryRequest(
                Q: q,
                Types: SplitCsv(type),
                Genres: ParseIntCsv(genre),
                Sort: sort ?? "added",
                Dir: dir ?? "desc",
                Page: page ?? 1,
                PageSize: pageSize ?? 48);
            return Results.Ok(await titles.QueryFavoritesAsync(profileId, request, ct));
        });

        group.MapGet("/types", async (HttpContext http, PickarrDbContext db, ITitleQueryService titles, CancellationToken ct) =>
        {
            var (profileId, error) = await ProfileRequest.RequirePersonalAsync(http, db, ct);
            return error ?? Results.Ok(await titles.ListFavoriteTypesAsync(profileId, ct));
        });

        group.MapGet("/genres", async (HttpContext http, PickarrDbContext db, ITitleQueryService titles, CancellationToken ct) =>
        {
            var (profileId, error) = await ProfileRequest.RequirePersonalAsync(http, db, ct);
            return error ?? Results.Ok(await titles.ListFavoriteGenresAsync(profileId, ct));
        });

        group.MapPut("/{titleId:int}", async (int titleId, HttpContext http, IFavoriteService favorites, PickarrDbContext db, CancellationToken ct) =>
        {
            var (profileId, error) = await ProfileRequest.RequirePersonalAsync(http, db, ct);
            if (error is not null)
            {
                return error;
            }

            try
            {
                await favorites.AddAsync(profileId, titleId, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        group.MapDelete("/{titleId:int}", async (int titleId, HttpContext http, IFavoriteService favorites, PickarrDbContext db, CancellationToken ct) =>
        {
            var (profileId, error) = await ProfileRequest.RequirePersonalAsync(http, db, ct);
            if (error is not null)
            {
                return error;
            }

            await favorites.RemoveAsync(profileId, titleId, ct);
            return Results.NoContent();
        });

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
