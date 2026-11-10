using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Domain.Entities;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Catalog;

internal static class RecoProfileStates
{
    public static async Task<int?> AdminIdAsync(PickarrDbContext db, CancellationToken cancellationToken)
    {
        return await db.Profiles.AsNoTracking()
            .Where(profile => profile.IsAdmin)
            .Select(profile => (int?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static Task<RecoState?> FindNoTrackingAsync(
        PickarrDbContext db,
        int profileId,
        CancellationToken cancellationToken) =>
        db.RecoStates.AsNoTracking().FirstOrDefaultAsync(state => state.ProfileId == profileId, cancellationToken);

    public static async Task<RecoState?> FindAdminNoTrackingAsync(
        PickarrDbContext db,
        CancellationToken cancellationToken)
    {
        var adminId = await AdminIdAsync(db, cancellationToken);
        return adminId is int id
            ? await FindNoTrackingAsync(db, id, cancellationToken)
            : null;
    }

    public static async Task<RecoState> GetOrCreateAsync(
        PickarrDbContext db,
        int profileId,
        CancellationToken cancellationToken)
    {
        var state = await db.RecoStates.FirstOrDefaultAsync(row => row.ProfileId == profileId, cancellationToken);
        if (state is not null)
        {
            return state;
        }

        state = new RecoState { ProfileId = profileId };
        db.RecoStates.Add(state);
        return state;
    }

    public static async Task<RecoState?> GetOrCreateAdminAsync(
        PickarrDbContext db,
        CancellationToken cancellationToken)
    {
        var adminId = await AdminIdAsync(db, cancellationToken);
        return adminId is int id
            ? await GetOrCreateAsync(db, id, cancellationToken)
            : null;
    }
}
