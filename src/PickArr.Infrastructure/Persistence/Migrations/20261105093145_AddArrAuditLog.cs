using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PickArr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArrAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArrAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    At = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ArrId = table.Column<int>(type: "INTEGER", nullable: true),
                    TitleId = table.Column<int>(type: "INTEGER", nullable: true),
                    Options = table.Column<string>(type: "TEXT", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArrAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArrAuditLogs_At",
                table: "ArrAuditLogs",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_ArrAuditLogs_TitleId",
                table: "ArrAuditLogs",
                column: "TitleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArrAuditLogs");
        }
    }
}
