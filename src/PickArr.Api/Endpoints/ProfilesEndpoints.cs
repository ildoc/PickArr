using Pickarr.Core.Abstractions;
using Pickarr.Core.Profiles;

namespace Pickarr.Api.Endpoints;

public static class ProfilesEndpoints
{
    public static IEndpointRouteBuilder MapProfilesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/profiles");

        group.MapGet("", async (IProfileService profiles, CancellationToken ct) =>
            Results.Ok(await profiles.ListAsync(ct)));

        group.MapPost("", async (
            ProfileCreateRequest? body,
            HttpContext http,
            IProfileService profiles,
            CancellationToken ct) =>
        {
            if (body is null)
            {
                return Results.BadRequest(new { code = ProfileCommandCodes.Invalid, detail = "Profile body is required." });
            }

            try
            {
                var created = await profiles.CreateAsync(ProfileRequest.ReadActorId(http), body, ct);
                return Results.Created($"/api/v1/profiles/{created.Id}", created);
            }
            catch (ProfileCommandException ex)
            {
                return Map(ex);
            }
        });

        group.MapPatch("/{id:int}", async (
            int id,
            ProfileUpdateRequest? body,
            HttpContext http,
            IProfileService profiles,
            CancellationToken ct) =>
        {
            if (body is null)
            {
                return Results.BadRequest(new { code = ProfileCommandCodes.Invalid, detail = "Profile body is required." });
            }

            try
            {
                return Results.Ok(await profiles.UpdateAsync(ProfileRequest.ReadActorId(http), id, body, ct));
            }
            catch (ProfileCommandException ex)
            {
                return Map(ex);
            }
        });

        group.MapDelete("/{id:int}", async (
            int id,
            HttpContext http,
            IProfileService profiles,
            CancellationToken ct) =>
        {
            try
            {
                await profiles.DeleteAsync(ProfileRequest.ReadActorId(http), id, ct);
                return Results.NoContent();
            }
            catch (ProfileCommandException ex)
            {
                return Map(ex);
            }
        });

        return endpoints;
    }

    private static IResult Map(ProfileCommandException exception) =>
        exception.Code switch
        {
            ProfileCommandCodes.Forbidden => Results.Json(
                new { code = exception.Code, detail = exception.Message },
                statusCode: StatusCodes.Status403Forbidden),
            ProfileCommandCodes.NotFound => Results.NotFound(new { code = exception.Code, detail = exception.Message }),
            ProfileCommandCodes.Limit or ProfileCommandCodes.AdminUndeletable => Results.Json(
                new { code = exception.Code, detail = exception.Message },
                statusCode: StatusCodes.Status409Conflict),
            _ => Results.BadRequest(new { code = exception.Code, detail = exception.Message }),
        };
}
