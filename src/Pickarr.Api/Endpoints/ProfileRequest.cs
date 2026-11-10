using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Profiles;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Api.Endpoints;

internal static class ProfileRequest
{
    public static int? ReadActorId(HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.TryGetValue(ProfileHeader.Name, out var raw))
        {
            return null;
        }

        return int.TryParse(raw.ToString(), out var id) && id > 0 ? id : null;
    }

    public static async Task<(int ProfileId, IResult? Error)> RequirePersonalAsync(
        HttpContext httpContext,
        PickarrDbContext db,
        CancellationToken cancellationToken)
    {
        if (ReadActorId(httpContext) is not int profileId)
        {
            return (0, Results.BadRequest(new
            {
                code = "PROFILE_REQUIRED",
                detail = "Header X-Pickarr-Profile is required.",
            }));
        }

        var exists = await db.Profiles.AsNoTracking()
            .AnyAsync(profile => profile.Id == profileId, cancellationToken);
        if (!exists)
        {
            return (0, Results.BadRequest(new
            {
                code = "PROFILE_UNKNOWN",
                detail = "Header X-Pickarr-Profile does not match a profile.",
            }));
        }

        return (profileId, null);
    }

    public static async Task<(int? ProfileId, IResult? Error)> ReadOptionalPersonalAsync(
        HttpContext httpContext,
        PickarrDbContext db,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Headers.ContainsKey(ProfileHeader.Name))
        {
            return (null, null);
        }

        if (ReadActorId(httpContext) is not int profileId
            || !await db.Profiles.AsNoTracking().AnyAsync(profile => profile.Id == profileId, cancellationToken))
        {
            return (null, Results.BadRequest(new
            {
                code = "PROFILE_UNKNOWN",
                detail = "Header X-Pickarr-Profile does not match a profile.",
            }));
        }

        return (profileId, null);
    }

    public static async Task<int?> ReadAttributedIdAsync(
        HttpContext httpContext,
        PickarrDbContext db,
        CancellationToken cancellationToken)
    {
        if (ReadActorId(httpContext) is not int profileId)
        {
            return null;
        }

        var exists = await db.Profiles.AsNoTracking()
            .AnyAsync(profile => profile.Id == profileId, cancellationToken);
        return exists ? profileId : null;
    }
}
