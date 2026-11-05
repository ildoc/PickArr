using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PickArr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTitlesAvailabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Titles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WatchmodeId = table.Column<int>(type: "INTEGER", nullable: true),
                    TmdbId = table.Column<int>(type: "INTEGER", nullable: true),
                    TmdbType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    ImdbId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    TvdbId = table.Column<int>(type: "INTEGER", nullable: true),
                    MediaType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    OriginalTitle = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ReleaseYear = table.Column<int>(type: "INTEGER", nullable: true),
                    MetadataStatus = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    MetadataFetchedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MetadataAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Titles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Availabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TitleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CatalogSourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsBaseline = table.Column<bool>(type: "INTEGER", nullable: false),
                    MissedFullScans = table.Column<int>(type: "INTEGER", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LeavingDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Availabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Availabilities_CatalogSources_CatalogSourceId",
                        column: x => x.CatalogSourceId,
                        principalTable: "CatalogSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Availabilities_Titles_TitleId",
                        column: x => x.TitleId,
                        principalTable: "Titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AvailabilityEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AvailabilityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SyncRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailabilityEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvailabilityEvents_Availabilities_AvailabilityId",
                        column: x => x.AvailabilityId,
                        principalTable: "Availabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Availabilities_CatalogSourceId_RemovedAt_AddedAt",
                table: "Availabilities",
                columns: new[] { "CatalogSourceId", "RemovedAt", "AddedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Availabilities_TitleId_CatalogSourceId",
                table: "Availabilities",
                columns: new[] { "TitleId", "CatalogSourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityEvents_AvailabilityId",
                table: "AvailabilityEvents",
                column: "AvailabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityEvents_OccurredAt",
                table: "AvailabilityEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_Titles_ImdbId",
                table: "Titles",
                column: "ImdbId");

            migrationBuilder.CreateIndex(
                name: "IX_Titles_MetadataStatus",
                table: "Titles",
                column: "MetadataStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Titles_ReleaseYear",
                table: "Titles",
                column: "ReleaseYear");

            migrationBuilder.CreateIndex(
                name: "IX_Titles_TmdbType_TmdbId",
                table: "Titles",
                columns: new[] { "TmdbType", "TmdbId" },
                unique: true,
                filter: "\"TmdbId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Titles_WatchmodeId",
                table: "Titles",
                column: "WatchmodeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvailabilityEvents");

            migrationBuilder.DropTable(
                name: "Availabilities");

            migrationBuilder.DropTable(
                name: "Titles");
        }
    }
}
