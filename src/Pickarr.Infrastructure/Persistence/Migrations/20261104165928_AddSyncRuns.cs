using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pickarr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CatalogSourceId = table.Column<int>(type: "INTEGER", nullable: true),
                    TriggeredBy = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    PagesFetched = table.Column<int>(type: "INTEGER", nullable: false),
                    CreditsUsed = table.Column<int>(type: "INTEGER", nullable: false),
                    TitlesAdded = table.Column<int>(type: "INTEGER", nullable: false),
                    TitlesUpdated = table.Column<int>(type: "INTEGER", nullable: false),
                    TitlesRemoved = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CheckpointJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncRuns_CatalogSources_CatalogSourceId",
                        column: x => x.CatalogSourceId,
                        principalTable: "CatalogSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncRuns_CatalogSourceId",
                table: "SyncRuns",
                column: "CatalogSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncRuns_Kind_CatalogSourceId_Status",
                table: "SyncRuns",
                columns: new[] { "Kind", "CatalogSourceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncRuns_StartedAt",
                table: "SyncRuns",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncRuns");
        }
    }
}
