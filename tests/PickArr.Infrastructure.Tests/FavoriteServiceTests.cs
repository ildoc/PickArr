using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Infrastructure.Catalog;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class FavoriteServiceTests
{
    [Fact]
    public async Task AddAsync_throws_when_title_is_missing()
    {
        await using var db = await CreateDbAsync();
        var sut = new FavoriteService(db, new FixedClock(DateTime.UtcNow));

        var error = await Should.ThrowAsync<KeyNotFoundException>(() => sut.AddAsync(42));
        error.Message.ShouldContain("42");
        (await db.Favorites.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RemoveAsync_is_a_no_op_when_favorite_is_absent()
    {
        await using var db = await CreateDbAsync();
        var sut = new FavoriteService(db, new FixedClock(DateTime.UtcNow));

        await sut.RemoveAsync(99);
        (await db.Favorites.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AddAsync_creates_favorite_for_existing_title()
    {
        await using var db = await CreateDbAsync();
        var now = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        db.Titles.Add(new Title
        {
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = "Fav",
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        var titleId = db.Titles.Single().Id;
        var sut = new FavoriteService(db, new FixedClock(now));

        await sut.AddAsync(titleId);

        var favorite = await db.Favorites.SingleAsync();
        favorite.TitleId.ShouldBe(titleId);
        favorite.CreatedAt.ShouldBe(now);
    }

    private static async Task<PickarrDbContext> CreateDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickarrDbContext>().UseSqlite(connection).Options;
        var db = new PickarrDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
