using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain.Entities;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Catalog;

public sealed class FavoriteService(PickarrDbContext db, IClock clock) : IFavoriteService
{
    public async Task AddAsync(int titleId, CancellationToken cancellationToken = default)
    {
        var exists = await db.Titles.AnyAsync(t => t.Id == titleId, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException($"Title {titleId} not found.");
        }

        var favorite = await db.Favorites.FindAsync([titleId], cancellationToken);
        if (favorite is not null)
        {
            return;
        }

        db.Favorites.Add(new Favorite
        {
            TitleId = titleId,
            CreatedAt = clock.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(int titleId, CancellationToken cancellationToken = default)
    {
        var favorite = await db.Favorites.FindAsync([titleId], cancellationToken);
        if (favorite is null)
        {
            return;
        }

        db.Favorites.Remove(favorite);
        await db.SaveChangesAsync(cancellationToken);
    }
}
