using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain.Entities;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Catalog;

public sealed class WatchedService(
    PickarrDbContext db,
    IClock clock,
    IRecoRebuildScheduler rebuildScheduler) : IWatchedService
{
    public async Task AddAsync(int profileId, int titleId, CancellationToken cancellationToken = default)
    {
        var exists = await db.Titles.AnyAsync(t => t.Id == titleId, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException($"Title {titleId} not found.");
        }

        var watched = await db.Watched.FindAsync([profileId, titleId], cancellationToken);
        if (watched is not null)
        {
            return;
        }

        db.Watched.Add(new Watched
        {
            ProfileId = profileId,
            TitleId = titleId,
            WatchedAt = clock.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        rebuildScheduler.ScheduleDebouncedRebuild(profileId);
    }

    public async Task RemoveAsync(int profileId, int titleId, CancellationToken cancellationToken = default)
    {
        var watched = await db.Watched.FindAsync([profileId, titleId], cancellationToken);
        if (watched is null)
        {
            return;
        }

        db.Watched.Remove(watched);
        await db.SaveChangesAsync(cancellationToken);
        rebuildScheduler.ScheduleDebouncedRebuild(profileId);
    }
}
