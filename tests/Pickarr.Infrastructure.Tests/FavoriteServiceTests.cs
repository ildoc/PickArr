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

        var error = await Should.ThrowAsync<KeyNotFoundException>(() => sut.AddAsync(1, 42));
        error.Message.ShouldContain("42");
        (await db.Favorites.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RemoveAsync_is_a_no_op_when_favorite_is_absent()
    {
        await using var db = await CreateDbAsync();
        var sut = new FavoriteService(db, new FixedClock(DateTime.UtcNow));

        await sut.RemoveAsync(1, 99);
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
        var profile = await AddProfileAsync(db, "One", now);
        var sut = new FavoriteService(db, new FixedClock(now));

        await sut.AddAsync(profile.Id, titleId);

        var favorite = await db.Favorites.SingleAsync();
        favorite.TitleId.ShouldBe(titleId);
        favorite.ProfileId.ShouldBe(profile.Id);
        favorite.CreatedAt.ShouldBe(now);
    }

    [Fact]
    public async Task Add_and_remove_are_scoped_to_the_profile()
    {
        await using var db = await CreateDbAsync();
        var now = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        db.Titles.Add(new Title
        {
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = "Shared",
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        });
        var first = await AddProfileAsync(db, "One", now);
        var second = await AddProfileAsync(db, "Two", now);
        await db.SaveChangesAsync();
        var titleId = db.Titles.Single().Id;
        var sut = new FavoriteService(db, new FixedClock(now));

        await sut.AddAsync(first.Id, titleId);
        await sut.AddAsync(second.Id, titleId);
        (await db.Favorites.CountAsync()).ShouldBe(2);

        await sut.RemoveAsync(first.Id, titleId);

        var remaining = await db.Favorites.SingleAsync();
        remaining.ProfileId.ShouldBe(second.Id);
        remaining.TitleId.ShouldBe(titleId);
    }

    private static async Task<Profile> AddProfileAsync(PickarrDbContext db, string name, DateTime now)
    {
        var profile = new Profile
        {
            Name = name,
            Color = Profile.DefaultColor,
            CreatedAt = now,
        };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();
        return profile;
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
