using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pickarr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArrLibraryCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArrLibraryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ArrId = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbId = table.Column<int>(type: "INTEGER", nullable: true),
                    TvdbId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImdbId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    TitleId = table.Column<int>(type: "INTEGER", nullable: true),
                    ArrTitle = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: true),
                    Monitored = table.Column<bool>(type: "INTEGER", nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    QueueProgress = table.Column<double>(type: "REAL", nullable: true),
                    SizeOnDisk = table.Column<long>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    EpisodeFileCount = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeCount = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalEpisodeCount = table.Column<int>(type: "INTEGER", nullable: true),
                    HasFile = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsAvailable = table.Column<bool>(type: "INTEGER", nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArrLibraryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArrLibraryItems_Titles_TitleId",
                        column: x => x.TitleId,
                        principalTable: "Titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ArrSeasonStates",
                columns: table => new
                {
                    ArrLibraryItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Monitored = table.Column<bool>(type: "INTEGER", nullable: false),
                    EpisodeFileCount = table.Column<int>(type: "INTEGER", nullable: false),
                    EpisodeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalEpisodeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SizeOnDisk = table.Column<long>(type: "INTEGER", nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    QueueProgress = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArrSeasonStates", x => new { x.ArrLibraryItemId, x.SeasonNumber });
                    table.ForeignKey(
                        name: "FK_ArrSeasonStates_ArrLibraryItems_ArrLibraryItemId",
                        column: x => x.ArrLibraryItemId,
                        principalTable: "ArrLibraryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArrLibraryItems_Kind_ArrId",
                table: "ArrLibraryItems",
                columns: new[] { "Kind", "ArrId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArrLibraryItems_TitleId",
                table: "ArrLibraryItems",
                column: "TitleId");

            migrationBuilder.CreateIndex(
                name: "IX_ArrLibraryItems_TmdbId",
                table: "ArrLibraryItems",
                column: "TmdbId");

            migrationBuilder.CreateIndex(
                name: "IX_ArrLibraryItems_TvdbId",
                table: "ArrLibraryItems",
                column: "TvdbId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArrSeasonStates");

            migrationBuilder.DropTable(
                name: "ArrLibraryItems");
        }
    }
}
