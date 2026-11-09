using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pickarr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recommendations",
                columns: table => new
                {
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    TitleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    Personal = table.Column<double>(type: "REAL", nullable: true),
                    Prior = table.Column<double>(type: "REAL", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    ReasonsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recommendations", x => new { x.Version, x.TitleId });
                    table.ForeignKey(
                        name: "FK_Recommendations_Titles_TitleId",
                        column: x => x.TitleId,
                        principalTable: "Titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecoState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CurrentVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    BuiltAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RatingCount = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectiveN = table.Column<double>(type: "REAL", nullable: false),
                    Stage = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ModelStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ModelId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    EmbeddingCoverage = table.Column<double>(type: "REAL", nullable: false),
                    ProfileJson = table.Column<string>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoState", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_TitleId",
                table: "Recommendations",
                column: "TitleId");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_Version_Score",
                table: "Recommendations",
                columns: new[] { "Version", "Score" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Recommendations");

            migrationBuilder.DropTable(
                name: "RecoState");
        }
    }
}
