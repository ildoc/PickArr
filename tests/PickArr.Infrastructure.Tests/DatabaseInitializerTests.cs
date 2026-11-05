using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PickArr.Core.Abstractions;
using PickArr.Core.Catalog;
using PickArr.Infrastructure.Persistence;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

public sealed class DatabaseInitializerTests
{
    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    [Fact]
    public async Task InitializeAsync_seeds_netflix_it_us_idempotently()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PickArrDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new PickArrDbContext(options);
        var initializer = new DatabaseInitializer(db, new FixedClock(DateTime.UtcNow), NullLogger<DatabaseInitializer>.Instance);
        await initializer.InitializeAsync();
        await initializer.InitializeAsync();

        (await db.Providers.CountAsync()).ShouldBe(CatalogOffers.Providers.Count);
        (await db.CatalogSources.CountAsync()).ShouldBe(CatalogOffers.Providers.Sum(provider => provider.Countries.Count));
        var providers = await db.Providers.Include(provider => provider.CatalogSources).ToListAsync();
        foreach (var offer in CatalogOffers.Providers)
        {
            var provider = providers.Single(item => item.Key == offer.Key);
            provider.DisplayName.ShouldBe(offer.DisplayName);
            provider.ExternalId.ShouldBe(offer.ExternalId);
            provider.IsEnabled.ShouldBe(offer.EnabledByDefault);
            provider.CatalogSources.Select(source => source.CountryCode).Order()
                .ShouldBe(offer.Countries.Order());
            provider.CatalogSources.ShouldAllBe(source => source.IsEnabled == false);
        }
    }
}
