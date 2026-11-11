using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Catalog;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Infrastructure.Catalog;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class CatalogUpserterTests
{
    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    [Fact]
    public async Task Upsert_is_idempotent_and_skips_Added_events_before_baseline()
    {
        await using var db = await CreateDbAsync();
        var source = await SeedSourceAsync(db, baselineCompleted: false);
        var upserter = new CatalogUpserter(db, new FixedClock(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc)));

        var entries = new[]
        {
            new CatalogEntry("1000001", "Example Movie Alpha", 2024, CatalogEntryType.Movie, 900001, "movie", "tt9000001"),
            new CatalogEntry("1000002", "Example Series Beta", 2023, CatalogEntryType.TvSeries, 900002, "tv", "tt9000002"),
        };

        var first = await upserter.UpsertPageAsync(source.Id, entries, Guid.NewGuid(), baselineCompleted: false);
        first.TitlesAdded.ShouldBe(2);
        first.AvailabilitiesAdded.ShouldBe(2);
        (await db.AvailabilityEvents.CountAsync()).ShouldBe(0);

        var second = await upserter.UpsertPageAsync(source.Id, entries, Guid.NewGuid(), baselineCompleted: false);
        second.TitlesAdded.ShouldBe(0);
        second.AvailabilitiesAdded.ShouldBe(0);
        (await db.Titles.CountAsync()).ShouldBe(2);
        (await db.Availabilities.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Upsert_ignores_placeholder_tmdb_ids()
    {
        await using var db = await CreateDbAsync();
        var source = await SeedSourceAsync(db, baselineCompleted: false);
        var upserter = new CatalogUpserter(db, new FixedClock(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc)));

        var result = await upserter.UpsertPageAsync(
            source.Id,
            [
                new CatalogEntry("100", "Unknown One", 2024, CatalogEntryType.Movie, 0, "", null),
                new CatalogEntry("200", "Unknown Two", 2023, CatalogEntryType.Movie, 0, " ", null),
            ],
            Guid.NewGuid(),
            baselineCompleted: false);

        result.TitlesAdded.ShouldBe(2);
        var titles = await db.Titles.OrderBy(t => t.WatchmodeId).ToListAsync();
        titles.Count.ShouldBe(2);
        titles.ShouldAllBe(t => t.TmdbId == null && t.TmdbType == null);
        titles.ShouldAllBe(t => t.MetadataStatus == MetadataStatuses.Unmapped);
    }

    [Fact]
    public async Task Upsert_marks_Pending_when_imdb_present_without_tmdb()
    {
        await using var db = await CreateDbAsync();
        var source = await SeedSourceAsync(db, baselineCompleted: false);
        var upserter = new CatalogUpserter(db, new FixedClock(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc)));

        await upserter.UpsertPageAsync(
            source.Id,
            [new CatalogEntry("555954", "Dead-End Job", 2026, CatalogEntryType.TvSeries, 0, "", "tt36458668")],
            Guid.NewGuid(),
            baselineCompleted: false);

        var title = await db.Titles.SingleAsync();
        title.TmdbId.ShouldBeNull();
        title.ImdbId.ShouldBe("tt36458668");
        title.MetadataStatus.ShouldBe(MetadataStatuses.Pending);
    }

    [Fact]
    public async Task Upsert_merges_by_imdb_across_different_watchmode_ids()
    {
        await using var db = await CreateDbAsync();
        var us = await SeedSourceAsync(db, baselineCompleted: false, country: "US");
        var it = await SeedSourceAsync(db, baselineCompleted: false, country: "IT", reuseProvider: true);
        var upserter = new CatalogUpserter(db, new FixedClock(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc)));

        await upserter.UpsertPageAsync(
            it.Id,
            [new CatalogEntry("3257219", "Die Falle", 2026, CatalogEntryType.TvSeries, 283371, "tv", "tt36470958")],
            Guid.NewGuid(),
            baselineCompleted: false);

        await upserter.UpsertPageAsync(
            us.Id,
            [new CatalogEntry("3317128", "The Trap", 2026, CatalogEntryType.TvSeries, null, null, "tt36470958")],
            Guid.NewGuid(),
            baselineCompleted: false);

        (await db.Titles.CountAsync()).ShouldBe(1);
        var title = await db.Titles.Include(t => t.Availabilities).SingleAsync();
        title.ImdbId.ShouldBe("tt36470958");
        title.TmdbId.ShouldBe(283371);
        title.WatchmodeId.ShouldBe(3257219);
        title.Availabilities.Count.ShouldBe(2);
        title.Availabilities.Select(a => a.CatalogSourceId).OrderBy(id => id).ToArray()
            .ShouldBe(new[] { us.Id, it.Id }.OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task Upsert_reuses_one_title_when_page_shares_a_tmdb_id()
    {
        await using var db = await CreateDbAsync();
        var source = await SeedSourceAsync(db, baselineCompleted: false);
        var upserter = new CatalogUpserter(db, new FixedClock(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc)));

        var entries = new[]
        {
            new CatalogEntry("100", "Shared Movie", 2024, CatalogEntryType.Movie, 555, "movie", "tt0000555"),
            new CatalogEntry("200", "Shared Movie Again", 2024, CatalogEntryType.Movie, 555, "movie", "tt0000555"),
        };

        var result = await upserter.UpsertPageAsync(source.Id, entries, Guid.NewGuid(), baselineCompleted: false);

        result.TitlesAdded.ShouldBe(1);
        result.AvailabilitiesAdded.ShouldBe(1);
        (await db.Titles.CountAsync()).ShouldBe(1);
        (await db.Availabilities.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Upsert_links_second_entry_when_first_tmdb_id_is_still_pending()
    {
        await using var db = await CreateDbAsync();
        var source = await SeedSourceAsync(db, baselineCompleted: false);
        var clock = new FixedClock(new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc));
        var upserter = new CatalogUpserter(db, clock);

        await upserter.UpsertPageAsync(
            source.Id,
            [new CatalogEntry("100", "No Tmdb Yet", 2020, CatalogEntryType.Movie, null, null, null)],
            Guid.NewGuid(),
            baselineCompleted: false);

        var result = await upserter.UpsertPageAsync(
            source.Id,
            [
                new CatalogEntry("100", "No Tmdb Yet", 2020, CatalogEntryType.Movie, 555, "movie", "tt0000555"),
                new CatalogEntry("200", "Same Tmdb", 2020, CatalogEntryType.Movie, 555, "movie", "tt0000555"),
            ],
            Guid.NewGuid(),
            baselineCompleted: false);

        result.TitlesAdded.ShouldBe(0);
        (await db.Titles.CountAsync()).ShouldBe(1);
        (await db.Titles.SingleAsync()).TmdbId.ShouldBe(555);
        (await db.Availabilities.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Upsert_after_baseline_emits_Added_and_Readded_events()
    {
        await using var db = await CreateDbAsync();
        var source = await SeedSourceAsync(db, baselineCompleted: true);
        var clock = new FixedClock(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc));
        var upserter = new CatalogUpserter(db, clock);

        var entry = new CatalogEntry("42", "Gone Then Back", 2020, CatalogEntryType.Movie, 42, "movie", null);
        await upserter.UpsertPageAsync(source.Id, [entry], Guid.NewGuid(), baselineCompleted: true);
        (await db.AvailabilityEvents.CountAsync(e => e.Type == AvailabilityEventTypes.Added)).ShouldBe(1);

        var availability = await db.Availabilities.SingleAsync();
        availability.RemovedAt = clock.UtcNow;
        await db.SaveChangesAsync();

        var result = await upserter.UpsertPageAsync(source.Id, [entry], Guid.NewGuid(), baselineCompleted: true);
        result.AvailabilitiesReadded.ShouldBe(1);
        (await db.AvailabilityEvents.CountAsync(e => e.Type == AvailabilityEventTypes.Readded)).ShouldBe(1);
        (await db.Availabilities.SingleAsync()).RemovedAt.ShouldBeNull();
        (await db.Titles.SingleAsync()).MetadataStatus.ShouldBe(MetadataStatuses.Pending);
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

    private static async Task<CatalogSource> SeedSourceAsync(
        PickarrDbContext db,
        bool baselineCompleted,
        string country = "US",
        bool reuseProvider = false)
    {
        Provider provider;
        if (reuseProvider)
        {
            provider = await db.Providers.SingleAsync(p => p.Key == "netflix");
        }
        else
        {
            provider = new Provider
            {
                Key = "netflix",
                DisplayName = "Netflix",
                ProviderType = "watchmode",
                ExternalId = "203",
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
            };
            db.Providers.Add(provider);
            await db.SaveChangesAsync();
        }

        var source = new CatalogSource
        {
            ProviderId = provider.Id,
            CountryCode = country,
            MetadataLanguage = country == "IT" ? "it-IT" : "en-US",
            IsEnabled = true,
            BaselineCompletedAt = baselineCompleted ? DateTime.UtcNow : null,
        };
        db.CatalogSources.Add(source);
        await db.SaveChangesAsync();
        return source;
    }
}
