using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Infrastructure.Catalog;
using PickArr.Infrastructure.Persistence;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

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

    private static async Task<PickArrDbContext> CreateDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickArrDbContext>().UseSqlite(connection).Options;
        var db = new PickArrDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
