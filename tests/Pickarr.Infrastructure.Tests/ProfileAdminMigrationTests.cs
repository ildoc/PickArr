using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Catalog;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class ProfileAdminMigrationTests
{
    private const string SchemaBeforeProfiles = "20261109131734_AddWatched";

    [Fact]
    public async Task Migrate_assigns_existing_personal_rows_to_the_admin()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickarrDbContext>().UseSqlite(connection).Options;

        await using (var db = new PickarrDbContext(options))
        {
            await db.Database.MigrateAsync(SchemaBeforeProfiles);
            await InsertLegacyRowsAsync(db);
        }

        await using (var db = new PickarrDbContext(options))
        {
            await db.Database.MigrateAsync();

            var admins = await db.Profiles.Where(profile => profile.IsAdmin).ToListAsync();
            admins.Count.ShouldBe(1);
            var admin = admins[0];
            admin.Name.ShouldBe(Profile.AdminName);
            admin.Color.ShouldBe(Profile.DefaultColor);
            admin.UseImplicit.ShouldBeFalse();
            admin.ExcludeInLibrary.ShouldBeFalse();

            (await db.Favorites.SingleAsync()).ProfileId.ShouldBe(admin.Id);
            (await db.Watched.SingleAsync()).ProfileId.ShouldBe(admin.Id);
            (await db.Ratings.SingleAsync()).ProfileId.ShouldBe(admin.Id);
            (await db.RatingEvents.SingleAsync()).ProfileId.ShouldBe(admin.Id);
            (await db.Recommendations.SingleAsync()).ProfileId.ShouldBe(admin.Id);
            (await db.RecoStates.SingleAsync()).ProfileId.ShouldBe(admin.Id);
            (await db.RecoEventLogs.SingleAsync()).ProfileId.ShouldBe(admin.Id);
            (await db.ArrAuditLogs.SingleAsync()).ProfileId.ShouldBe(admin.Id);
            (await db.ArrLibraryItems.SingleAsync()).ArrTitle.ShouldBe("Kept");
            (await db.TitleEmbeddings.SingleAsync()).ModelId.ShouldBe("model");

            foreach (var table in new[] { "ArrLibraryItems", "Titles", "Availabilities", "TitleEmbeddings", "Settings" })
            {
                (await HasColumnAsync(db, table, "ProfileId")).ShouldBeFalse(table);
            }
        }

        await using (var db = new PickarrDbContext(options))
        {
            var initializer = CreateInitializer(db, DateTime.UtcNow);
            await initializer.EnsureAdminProfileAsync();
            (await db.Profiles.CountAsync(profile => profile.IsAdmin)).ShouldBe(1);
            (await db.Profiles.SingleAsync(profile => profile.IsAdmin)).UseImplicit.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task EnsureCreated_seed_creates_one_admin_and_schema_rejects_a_second()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickarrDbContext>().UseSqlite(connection).Options;
        var now = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc);

        await using var db = new PickarrDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Settings.Add(new AppSetting
        {
            Key = SettingsKeys.RecoUseImplicit,
            ValueJson = "false",
            UpdatedAt = now,
        });
        db.Settings.Add(new AppSetting
        {
            Key = SettingsKeys.RecoExcludeInLibrary,
            ValueJson = "true",
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var initializer = CreateInitializer(db, now);
        await initializer.EnsureAdminProfileAsync();
        await initializer.EnsureAdminProfileAsync();

        var admin = await db.Profiles.SingleAsync();
        admin.IsAdmin.ShouldBeTrue();
        admin.Name.ShouldBe(Profile.AdminName);
        admin.Color.ShouldBe(Profile.DefaultColor);
        admin.UseImplicit.ShouldBeFalse();
        admin.ExcludeInLibrary.ShouldBeTrue();
        admin.CreatedAt.ShouldBe(now);

        db.Profiles.Add(new Profile
        {
            Name = "Guest",
            Color = Profile.DefaultColor,
            IsAdmin = false,
            CreatedAt = now,
        });
        await db.SaveChangesAsync();
        (await db.Profiles.CountAsync()).ShouldBe(2);

        db.Profiles.Add(new Profile
        {
            Name = "Second",
            Color = Profile.DefaultColor,
            IsAdmin = true,
            CreatedAt = now,
        });
        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static async Task InsertLegacyRowsAsync(PickarrDbContext db)
    {
        const string at = "2026-11-01 00:00:00";
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Titles" ("MediaType", "Category", "MetadataStatus", "MetadataAttempts", "CreatedAt", "UpdatedAt")
            VALUES ('movie', 'movie', 'Ready', 0, {0}, {0});
            """,
            at);
        await db.Database.ExecuteSqlRawAsync(
            """INSERT INTO "Favorites" ("TitleId", "CreatedAt") VALUES (1, {0});""",
            at);
        await db.Database.ExecuteSqlRawAsync(
            """INSERT INTO "Watched" ("TitleId", "WatchedAt") VALUES (1, {0});""",
            at);
        await db.Database.ExecuteSqlRawAsync(
            """INSERT INTO "Ratings" ("TitleId", "Value", "RatedAt") VALUES (1, 'Like', {0});""",
            at);
        await db.Database.ExecuteSqlRawAsync(
            """INSERT INTO "RatingEvents" ("TitleId", "Value", "At", "Origin") VALUES (1, 'Like', {0}, 'user');""",
            at);
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Recommendations" ("Version", "TitleId", "Score", "Prior", "Rank", "ReasonsJson")
            VALUES (1, 1, 0.5, 0.4, 1, '[]');
            """);
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "RecoState" ("Id", "CurrentVersion", "RatingCount", "EffectiveN", "Stage", "ModelStatus", "EmbeddingCoverage")
            VALUES (1, 1, 1, 1, 'cold', 'Absent', 0);
            """);
        await db.Database.ExecuteSqlRawAsync(
            """INSERT INTO "RecoEventLog" ("At", "TitleId", "Event") VALUES ({0}, 1, 'impression');""",
            at);
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "ArrAuditLogs" ("At", "Action", "Kind", "Options", "Outcome")
            VALUES ({0}, 'Add', 'movie', '{{}}', 'Success');
            """,
            at);
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "ArrLibraryItems" ("Kind", "ArrId", "ArrTitle", "Monitored", "State", "SizeOnDisk", "SyncedAt")
            VALUES ('movie', 1, 'Kept', 0, 'Downloaded', 0, {0});
            """,
            at);
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "TitleEmbeddings" ("TitleId", "ModelId", "Vector", "TextHash", "UpdatedAt")
            VALUES (1, 'model', X'00', 'hash', {0});
            """,
            at);
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Settings" ("Key", "ValueJson", "IsSecret", "UpdatedAt")
            VALUES ({0}, 'false', 0, {1});
            """,
            SettingsKeys.RecoUseImplicit,
            at);
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Settings" ("Key", "ValueJson", "IsSecret", "UpdatedAt")
            VALUES ({0}, 'false', 0, {1});
            """,
            SettingsKeys.RecoExcludeInLibrary,
            at);
    }

    private static async Task<bool> HasColumnAsync(PickarrDbContext db, string table, string column)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $column";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$column";
        parameter.Value = column;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result) > 0;
    }

    private static DatabaseInitializer CreateInitializer(PickarrDbContext db, DateTime now) =>
        new(
            db,
            new FixedClock(now),
            new TitleIdentityMaintenance(db, NullLogger<TitleIdentityMaintenance>.Instance),
            NullLogger<DatabaseInitializer>.Instance);

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
