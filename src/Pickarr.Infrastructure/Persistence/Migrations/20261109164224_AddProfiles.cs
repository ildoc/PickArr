using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Settings;

#nullable disable

namespace Pickarr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProfileId",
                table: "Watched",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfileId",
                table: "RecoState",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfileId",
                table: "Recommendations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfileId",
                table: "RecoEventLog",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfileId",
                table: "Ratings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfileId",
                table: "RatingEvents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfileId",
                table: "Favorites",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfileId",
                table: "ArrAuditLogs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsAdmin = table.Column<bool>(type: "INTEGER", nullable: false),
                    UseImplicit = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    ExcludeInLibrary = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Watched_ProfileId",
                table: "Watched",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoState_ProfileId",
                table: "RecoState",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_ProfileId",
                table: "Recommendations",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoEventLog_ProfileId",
                table: "RecoEventLog",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_ProfileId",
                table: "Ratings",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingEvents_ProfileId",
                table: "RatingEvents",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_ProfileId",
                table: "Favorites",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ArrAuditLogs_ProfileId",
                table: "ArrAuditLogs",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_IsAdmin",
                table: "Profiles",
                column: "IsAdmin",
                unique: true,
                filter: "\"IsAdmin\" = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_ArrAuditLogs_Profiles_ProfileId",
                table: "ArrAuditLogs",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Favorites_Profiles_ProfileId",
                table: "Favorites",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RatingEvents_Profiles_ProfileId",
                table: "RatingEvents",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Ratings_Profiles_ProfileId",
                table: "Ratings",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RecoEventLog_Profiles_ProfileId",
                table: "RecoEventLog",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Profiles_ProfileId",
                table: "Recommendations",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RecoState_Profiles_ProfileId",
                table: "RecoState",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Watched_Profiles_ProfileId",
                table: "Watched",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(
                $"""
                INSERT INTO "Profiles" ("Name", "Color", "IsAdmin", "UseImplicit", "ExcludeInLibrary", "CreatedAt")
                SELECT '{Profile.AdminName}', '{Profile.DefaultColor}', 1,
                  CASE WHEN (SELECT "ValueJson" FROM "Settings" WHERE "Key" = '{SettingsKeys.RecoUseImplicit}') = 'false' THEN 0 ELSE 1 END,
                  CASE WHEN (SELECT "ValueJson" FROM "Settings" WHERE "Key" = '{SettingsKeys.RecoExcludeInLibrary}') = 'false' THEN 0 ELSE 1 END,
                  '2026-11-09 00:00:00'
                WHERE NOT EXISTS (SELECT 1 FROM "Profiles" WHERE "IsAdmin" = 1);
                """);

            foreach (var table in new[]
            {
                "Favorites",
                "Watched",
                "Ratings",
                "RatingEvents",
                "Recommendations",
                "RecoState",
                "RecoEventLog",
                "ArrAuditLogs",
            })
            {
                migrationBuilder.Sql(
                    $"""
                    UPDATE "{table}"
                    SET "ProfileId" = (SELECT "Id" FROM "Profiles" WHERE "IsAdmin" = 1)
                    WHERE "ProfileId" IS NULL;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArrAuditLogs_Profiles_ProfileId",
                table: "ArrAuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Favorites_Profiles_ProfileId",
                table: "Favorites");

            migrationBuilder.DropForeignKey(
                name: "FK_RatingEvents_Profiles_ProfileId",
                table: "RatingEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Ratings_Profiles_ProfileId",
                table: "Ratings");

            migrationBuilder.DropForeignKey(
                name: "FK_RecoEventLog_Profiles_ProfileId",
                table: "RecoEventLog");

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Profiles_ProfileId",
                table: "Recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_RecoState_Profiles_ProfileId",
                table: "RecoState");

            migrationBuilder.DropForeignKey(
                name: "FK_Watched_Profiles_ProfileId",
                table: "Watched");

            migrationBuilder.DropTable(
                name: "Profiles");

            migrationBuilder.DropIndex(
                name: "IX_Watched_ProfileId",
                table: "Watched");

            migrationBuilder.DropIndex(
                name: "IX_RecoState_ProfileId",
                table: "RecoState");

            migrationBuilder.DropIndex(
                name: "IX_Recommendations_ProfileId",
                table: "Recommendations");

            migrationBuilder.DropIndex(
                name: "IX_RecoEventLog_ProfileId",
                table: "RecoEventLog");

            migrationBuilder.DropIndex(
                name: "IX_Ratings_ProfileId",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_RatingEvents_ProfileId",
                table: "RatingEvents");

            migrationBuilder.DropIndex(
                name: "IX_Favorites_ProfileId",
                table: "Favorites");

            migrationBuilder.DropIndex(
                name: "IX_ArrAuditLogs_ProfileId",
                table: "ArrAuditLogs");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "Watched");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "RecoState");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "Recommendations");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "RecoEventLog");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "Ratings");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "RatingEvents");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "Favorites");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "ArrAuditLogs");
        }
    }
}
