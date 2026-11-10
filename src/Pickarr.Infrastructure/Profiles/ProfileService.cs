using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Profiles;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Profiles;

public sealed class ProfileService(PickarrDbContext db, IClock clock) : IProfileService
{
    public async Task<IReadOnlyList<ProfileDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await db.Profiles.AsNoTracking()
            .OrderByDescending(profile => profile.IsAdmin)
            .ThenBy(profile => profile.Name)
            .ToListAsync(cancellationToken);
        return profiles.Select(ToDto).ToList();
    }

    public async Task<ProfileDto> CreateAsync(
        int? actorProfileId,
        ProfileCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAdminAsync(actorProfileId, cancellationToken))
        {
            throw new ProfileCommandException(
                ProfileCommandCodes.Forbidden,
                "Only the admin profile can create profiles.");
        }

        var count = await db.Profiles.CountAsync(cancellationToken);
        if (count >= Profile.MaxProfiles)
        {
            throw new ProfileCommandException(
                ProfileCommandCodes.Limit,
                $"At most {Profile.MaxProfiles} profiles are allowed.");
        }

        var name = RequireName(request.Name);
        await EnsureUniqueNameAsync(name, null, cancellationToken);
        var color = request.Color is null
            ? Profile.DefaultColor
            : RequireColor(request.Color);

        var profile = new Profile
        {
            Name = name,
            Color = color,
            IsAdmin = false,
            CreatedAt = clock.UtcNow,
        };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(profile);
    }

    public async Task<ProfileDto> UpdateAsync(
        int? actorProfileId,
        int profileId,
        ProfileUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(item => item.Id == profileId, cancellationToken);
        if (profile is null)
        {
            throw new ProfileCommandException(ProfileCommandCodes.NotFound, "Profile not found.");
        }

        var actorIsAdmin = await IsAdminAsync(actorProfileId, cancellationToken);
        if (actorProfileId != profileId && !actorIsAdmin)
        {
            throw new ProfileCommandException(
                ProfileCommandCodes.Forbidden,
                "Only the admin profile can edit another profile.");
        }

        if (request.IsAdmin is bool isAdmin && isAdmin != profile.IsAdmin)
        {
            throw new ProfileCommandException(
                ProfileCommandCodes.Invalid,
                "The admin role cannot be changed.");
        }

        if (request.Name is not null)
        {
            var name = RequireName(request.Name);
            await EnsureUniqueNameAsync(name, profile.Id, cancellationToken);
            profile.Name = name;
        }

        if (request.Color is not null)
        {
            profile.Color = RequireColor(request.Color);
        }

        if (request.UseImplicit is bool useImplicit)
        {
            profile.UseImplicit = useImplicit;
        }

        if (request.ExcludeInLibrary is bool excludeInLibrary)
        {
            profile.ExcludeInLibrary = excludeInLibrary;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(profile);
    }

    public async Task DeleteAsync(
        int? actorProfileId,
        int profileId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAdminAsync(actorProfileId, cancellationToken))
        {
            throw new ProfileCommandException(
                ProfileCommandCodes.Forbidden,
                "Only the admin profile can delete profiles.");
        }

        var profile = await db.Profiles.FirstOrDefaultAsync(item => item.Id == profileId, cancellationToken);
        if (profile is null)
        {
            throw new ProfileCommandException(ProfileCommandCodes.NotFound, "Profile not found.");
        }

        if (profile.IsAdmin)
        {
            throw new ProfileCommandException(
                ProfileCommandCodes.AdminUndeletable,
                "The admin profile cannot be deleted.");
        }

        await db.Recommendations.Where(row => row.ProfileId == profileId).ExecuteDeleteAsync(cancellationToken);
        await db.RecoEventLogs.Where(row => row.ProfileId == profileId).ExecuteDeleteAsync(cancellationToken);
        await db.RecoStates.Where(row => row.ProfileId == profileId).ExecuteDeleteAsync(cancellationToken);

        db.Profiles.Remove(profile);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> IsAdminAsync(int? actorProfileId, CancellationToken cancellationToken)
    {
        if (actorProfileId is not int id)
        {
            return false;
        }

        return await db.Profiles.AsNoTracking()
            .AnyAsync(profile => profile.Id == id && profile.IsAdmin, cancellationToken);
    }

    private async Task EnsureUniqueNameAsync(string name, int? exceptId, CancellationToken cancellationToken)
    {
        var names = await db.Profiles.AsNoTracking()
            .Where(profile => exceptId == null || profile.Id != exceptId)
            .Select(profile => profile.Name)
            .ToListAsync(cancellationToken);
        if (names.Any(existing => string.Equals(existing.Trim(), name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ProfileCommandException(ProfileCommandCodes.Invalid, "Profile name is already in use.");
        }
    }

    private static string RequireName(string? name)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length is < 1 or > Profile.MaxNameLength)
        {
            throw new ProfileCommandException(
                ProfileCommandCodes.Invalid,
                $"Name must be 1–{Profile.MaxNameLength} characters.");
        }

        return trimmed;
    }

    private static string RequireColor(string color)
    {
        if (!Profile.TryCanonicalColor(color, out var canonical))
        {
            throw new ProfileCommandException(ProfileCommandCodes.Invalid, "Color is not in the palette.");
        }

        return canonical;
    }

    private static ProfileDto ToDto(Profile profile) =>
        new(
            profile.Id,
            profile.Name,
            profile.Color,
            profile.IsAdmin,
            profile.UseImplicit,
            profile.ExcludeInLibrary,
            profile.CreatedAt);
}
