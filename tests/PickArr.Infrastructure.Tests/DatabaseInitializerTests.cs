using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Catalog;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

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

        var options = new DbContextOptionsBuilder<PickarrDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new PickarrDbContext(options);
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

        var recoEnabled = await db.Settings.SingleAsync(s => s.Key == SettingsKeys.RecoEnabled);
        recoEnabled.ValueJson.ShouldBe("true");
        var eventsEnabled = await db.Settings.SingleAsync(s => s.Key == SettingsKeys.RecoEventsEnabled);
        eventsEnabled.ValueJson.ShouldBe("false");
        var minRatings = await db.Settings.SingleAsync(s => s.Key == SettingsKeys.RecoMlMinRatings);
        minRatings.ValueJson.ShouldBe("100");
    }
}
