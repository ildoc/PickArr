using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PickArr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoEvalAndEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastEvalAt",
                table: "RecoState",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEvalJson",
                table: "RecoState",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Level3Active",
                table: "RecoState",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "RecoEventLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    At = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TitleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Event = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: true),
                    Rank = table.Column<int>(type: "INTEGER", nullable: true),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoEventLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecoEventLog_Titles_TitleId",
                        column: x => x.TitleId,
                        principalTable: "Titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecoEventLog_At",
                table: "RecoEventLog",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_RecoEventLog_TitleId_At",
                table: "RecoEventLog",
                columns: new[] { "TitleId", "At" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecoEventLog");

            migrationBuilder.DropColumn(
                name: "LastEvalAt",
                table: "RecoState");

            migrationBuilder.DropColumn(
                name: "LastEvalJson",
                table: "RecoState");

            migrationBuilder.DropColumn(
                name: "Level3Active",
                table: "RecoState");
        }
    }
}
