using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pickarr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopeRecommendationsByProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "Recommendations", "RecoState", "RecoEventLog" })
            {
                migrationBuilder.Sql(
                    $"""
                    UPDATE "{table}"
                    SET "ProfileId" = (SELECT "Id" FROM "Profiles" WHERE "IsAdmin" = 1)
                    WHERE "ProfileId" IS NULL;
                    """);
            }

            migrationBuilder.DropForeignKey(
                name: "FK_RecoEventLog_Profiles_ProfileId",
                table: "RecoEventLog");

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Profiles_ProfileId",
                table: "Recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_RecoState_Profiles_ProfileId",
                table: "RecoState");

            migrationBuilder.DropIndex(
                name: "IX_RecoState_ProfileId",
                table: "RecoState");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Recommendations",
                table: "Recommendations");

            migrationBuilder.DropIndex(
                name: "IX_Recommendations_ProfileId",
                table: "Recommendations");

            migrationBuilder.AlterColumn<int>(
                name: "ProfileId",
                table: "RecoState",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProfileId",
                table: "Recommendations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProfileId",
                table: "RecoEventLog",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Recommendations",
                table: "Recommendations",
                columns: new[] { "ProfileId", "Version", "TitleId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecoState_ProfileId",
                table: "RecoState",
                column: "ProfileId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RecoEventLog_Profiles_ProfileId",
                table: "RecoEventLog",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Recommendations_Profiles_ProfileId",
                table: "Recommendations",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RecoState_Profiles_ProfileId",
                table: "RecoState",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecoEventLog_Profiles_ProfileId",
                table: "RecoEventLog");

            migrationBuilder.DropForeignKey(
                name: "FK_Recommendations_Profiles_ProfileId",
                table: "Recommendations");

            migrationBuilder.DropForeignKey(
                name: "FK_RecoState_Profiles_ProfileId",
                table: "RecoState");

            migrationBuilder.DropIndex(
                name: "IX_RecoState_ProfileId",
                table: "RecoState");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Recommendations",
                table: "Recommendations");

            migrationBuilder.AlterColumn<int>(
                name: "ProfileId",
                table: "RecoState",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ProfileId",
                table: "Recommendations",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ProfileId",
                table: "RecoEventLog",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Recommendations",
                table: "Recommendations",
                columns: new[] { "Version", "TitleId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecoState_ProfileId",
                table: "RecoState",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_ProfileId",
                table: "Recommendations",
                column: "ProfileId");

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
        }
    }
}
