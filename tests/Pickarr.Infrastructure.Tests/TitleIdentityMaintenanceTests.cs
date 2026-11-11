using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Infrastructure.Catalog;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class TitleIdentityMaintenanceTests
{
    [Fact]
    public async Task Run_requeues_Unmapped_with_Imdb_and_merges_duplicate_Imdb_rows()
    {
        await using var db = await CreateDbAsync();
        var now = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
        var provider = new Provider
        {
            Key = "netflix",
            DisplayName = "Netflix",
            ProviderType = "watchmode",
            ExternalId = "203",
            IsEnabled = true,
            CreatedAt = now,
        };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();

        var us = new CatalogSource
        {
            ProviderId = provider.Id,
            CountryCode = "US",
            MetadataLanguage = "en-US",
            IsEnabled = true,
        };
        var it = new CatalogSource
        {
            ProviderId = provider.Id,
            CountryCode = "IT",
            MetadataLanguage = "it-IT",
            IsEnabled = true,
        };
        db.CatalogSources.AddRange(us, it);
        await db.SaveChangesAsync();

        var ready = new Title
        {
            WatchmodeId = 3257219,
            TmdbId = 283371,
            TmdbType = "tv",
            ImdbId = "tt36470958",
            MediaType = MediaTypes.Series,
            Category = TitleCategories.Series,
            OriginalTitle = "The Trap",
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var unmappedDup = new Title
        {
            WatchmodeId = 3317128,
            ImdbId = "tt36470958",
            MediaType = MediaTypes.Series,
            Category = TitleCategories.Series,
            OriginalTitle = "The Trap",
            MetadataStatus = MetadataStatuses.Unmapped,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var unmappedSolo = new Title
        {
            WatchmodeId = 555954,
            ImdbId = "tt36458668",
            MediaType = MediaTypes.Series,
            Category = TitleCategories.Series,
            OriginalTitle = "Dead-End Job",
            MetadataStatus = MetadataStatuses.Unmapped,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Titles.AddRange(ready, unmappedDup, unmappedSolo);
        await db.SaveChangesAsync();

        db.Availabilities.AddRange(
            new Availability
            {
                TitleId = ready.Id,
                CatalogSourceId = it.Id,
                FirstSeenAt = now,
                LastSeenAt = now,
            },
            new Availability
            {
                TitleId = unmappedDup.Id,
                CatalogSourceId = us.Id,
                FirstSeenAt = now,
                LastSeenAt = now,
            },
            new Availability
            {
                TitleId = unmappedSolo.Id,
                CatalogSourceId = us.Id,
                FirstSeenAt = now,
                LastSeenAt = now,
            });
        await db.SaveChangesAsync();

        var maintenance = new TitleIdentityMaintenance(db, NullLogger<TitleIdentityMaintenance>.Instance);
        await maintenance.RunAsync();

        (await db.Titles.CountAsync(t => t.ImdbId == "tt36470958")).ShouldBe(1);
        var survivor = await db.Titles.Include(t => t.Availabilities).SingleAsync(t => t.ImdbId == "tt36470958");
        survivor.MetadataStatus.ShouldBe(MetadataStatuses.Ready);
        survivor.TmdbId.ShouldBe(283371);
        survivor.WatchmodeId.ShouldBe(3257219);
        survivor.Availabilities.Select(a => a.CatalogSourceId).OrderBy(id => id).ToArray()
            .ShouldBe(new[] { us.Id, it.Id }.OrderBy(id => id).ToArray());

        var solo = await db.Titles.SingleAsync(t => t.ImdbId == "tt36458668");
        solo.MetadataStatus.ShouldBe(MetadataStatuses.Pending);
        solo.MetadataAttempts.ShouldBe(0);
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
}
