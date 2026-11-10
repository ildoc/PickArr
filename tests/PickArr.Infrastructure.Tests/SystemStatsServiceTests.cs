using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Infrastructure.Jobs;
using Pickarr.Infrastructure.Persistence;
using Pickarr.Infrastructure.Diagnostics;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class SystemStatsServiceTests
{
    [Fact]
    public async Task GetStatsAsync_counts_titles_by_provider_country_and_source()
    {
        await using var db = await CreateDbAsync();
        var netflix = await SeedProviderAsync(db, "netflix", "Netflix");
        var disney = await SeedProviderAsync(db, "disney", "Disney+");
        var netflixIt = await SeedSourceAsync(db, netflix, "IT");
        var netflixUs = await SeedSourceAsync(db, netflix, "US");
        var disneyUs = await SeedSourceAsync(db, disney, "US");

        var shared = await SeedTitleAsync(db, "Shared");
        var onlyIt = await SeedTitleAsync(db, "Only IT");
        var removed = await SeedTitleAsync(db, "Removed");

        await SeedAvailabilityAsync(db, shared, netflixIt);
        await SeedAvailabilityAsync(db, shared, netflixUs);
        await SeedAvailabilityAsync(db, onlyIt, netflixIt);
        await SeedAvailabilityAsync(db, shared, disneyUs);
        await SeedAvailabilityAsync(db, removed, netflixUs, removedAt: DateTime.UtcNow);

        var root = Path.Combine(Path.GetTempPath(), "pickarr-stats-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbFile = Path.Combine(root, "Pickarr.db");
        await File.WriteAllBytesAsync(dbFile, new byte[128]);
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        await File.WriteAllBytesAsync(Path.Combine(root, "logs", "a.log"), new byte[32]);

        try
        {
            var paths = new PickarrDataPaths(root, jobsDatabaseFile: Path.Combine(root, "jobs.db"));
            var hangfire = new HangfireRuntimeInfo(HangfireStorageKind.Memory, SqlitePath: null);
            var sut = new SystemStatsService(db, paths, hangfire);

            var stats = await sut.GetStatsAsync();

            stats.TotalTitles.ShouldBe(3);
            stats.ByProvider.ShouldContain(x => x.Name == "Netflix" && x.TitleCount == 2);
            stats.ByProvider.ShouldContain(x => x.Name == "Disney+" && x.TitleCount == 1);
            stats.ByCountry.ShouldContain(x => x.Name == "IT" && x.TitleCount == 2);
            stats.ByCountry.ShouldContain(x => x.Name == "US" && x.TitleCount == 1);
            stats.BySource.ShouldContain(x =>
                x.ProviderName == "Netflix" && x.CountryCode == "IT" && x.TitleCount == 2);
            stats.BySource.ShouldContain(x =>
                x.ProviderName == "Netflix" && x.CountryCode == "US" && x.TitleCount == 1);
            stats.Storage.Entries.ShouldContain(x => x.Key == "database" && x.Bytes == 128);
            stats.Storage.Entries.ShouldContain(x => x.Key == "logs" && x.Bytes == 32);
            stats.Storage.Entries.ShouldNotContain(x => x.Key == "jobs");
            stats.Storage.TotalBytes.ShouldBeGreaterThanOrEqualTo(160);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // best-effort cleanup
            }
        }
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

    private static async Task<Provider> SeedProviderAsync(PickarrDbContext db, string key, string name)
    {
        var provider = new Provider
        {
            Key = key,
            DisplayName = name,
            ProviderType = "watchmode",
            ExternalId = key,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
        return provider;
    }

    private static async Task<CatalogSource> SeedSourceAsync(PickarrDbContext db, Provider provider, string country)
    {
        var source = new CatalogSource
        {
            ProviderId = provider.Id,
            CountryCode = country,
            MetadataLanguage = "en-US",
            IsEnabled = true,
        };
        db.CatalogSources.Add(source);
        await db.SaveChangesAsync();
        return source;
    }

    private static async Task<Title> SeedTitleAsync(PickarrDbContext db, string name)
    {
        var title = new Title
        {
            WatchmodeId = Random.Shared.Next(1, 1_000_000),
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = name,
            OriginalTitleNormalized = name.ToLowerInvariant(),
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Titles.Add(title);
        await db.SaveChangesAsync();
        return title;
    }

    private static async Task SeedAvailabilityAsync(
        PickarrDbContext db,
        Title title,
        CatalogSource source,
        DateTime? removedAt = null)
    {
        var now = DateTime.UtcNow;
        db.Availabilities.Add(new Availability
        {
            TitleId = title.Id,
            CatalogSourceId = source.Id,
            FirstSeenAt = now,
            LastSeenAt = now,
            AddedAt = now,
            RemovedAt = removedAt,
        });
        await db.SaveChangesAsync();
    }
}
