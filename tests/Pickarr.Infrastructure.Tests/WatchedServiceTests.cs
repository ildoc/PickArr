using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Infrastructure.Catalog;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class WatchedServiceTests
{
    [Fact]
    public async Task Add_and_remove_are_scoped_to_the_profile()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickarrDbContext>().UseSqlite(connection).Options;
        await using var db = new PickarrDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        var title = new Title
        {
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = "Seen",
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var first = new Profile { Name = "One", Color = Profile.DefaultColor, CreatedAt = now };
        var second = new Profile { Name = "Two", Color = Profile.DefaultColor, CreatedAt = now };
        db.Titles.Add(title);
        db.Profiles.AddRange(first, second);
        await db.SaveChangesAsync();

        var sut = new WatchedService(db, new FixedClock(now), new NoopRebuild());
        await sut.AddAsync(first.Id, title.Id);
        await sut.AddAsync(second.Id, title.Id);
        (await db.Watched.CountAsync()).ShouldBe(2);

        await sut.RemoveAsync(first.Id, title.Id);

        var remaining = await db.Watched.SingleAsync();
        remaining.ProfileId.ShouldBe(second.Id);
        remaining.TitleId.ShouldBe(title.Id);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class NoopRebuild : IRecoRebuildScheduler
    {
        public void ScheduleDebouncedRebuild(int profileId)
        {
        }
    }
}
