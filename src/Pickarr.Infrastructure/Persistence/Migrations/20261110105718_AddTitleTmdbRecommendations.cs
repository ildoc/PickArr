using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pickarr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTitleTmdbRecommendations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TmdbRecommendationsFetchedAt",
                table: "Titles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TitleTmdbRecommendations",
                columns: table => new
                {
                    SourceTitleId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelatedTitleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TitleTmdbRecommendations", x => new { x.SourceTitleId, x.RelatedTitleId });
                    table.ForeignKey(
                        name: "FK_TitleTmdbRecommendations_Titles_RelatedTitleId",
                        column: x => x.RelatedTitleId,
                        principalTable: "Titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TitleTmdbRecommendations_Titles_SourceTitleId",
                        column: x => x.SourceTitleId,
                        principalTable: "Titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TitleTmdbRecommendations_RelatedTitleId",
                table: "TitleTmdbRecommendations",
                column: "RelatedTitleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TitleTmdbRecommendations");

            migrationBuilder.DropColumn(
                name: "TmdbRecommendationsFetchedAt",
                table: "Titles");
        }
    }
}
