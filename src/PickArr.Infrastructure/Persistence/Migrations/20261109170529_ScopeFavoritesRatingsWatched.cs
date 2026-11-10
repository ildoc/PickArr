using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pickarr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopeFavoritesRatingsWatched : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "Favorites", "Watched", "Ratings", "RatingEvents" })
            {
                migrationBuilder.Sql(
                    $"""
                    UPDATE "{table}"
                    SET "ProfileId" = (SELECT "Id" FROM "Profiles" WHERE "IsAdmin" = 1)
                    WHERE "ProfileId" IS NULL;
                    """);
            }

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
                name: "FK_Watched_Profiles_ProfileId",
                table: "Watched");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Watched",
                table: "Watched");

            migrationBuilder.DropIndex(
                name: "IX_Watched_ProfileId",
                table: "Watched");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ratings",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_Ratings_ProfileId",
                table: "Ratings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Favorites",
                table: "Favorites");

            migrationBuilder.DropIndex(
                name: "IX_Favorites_ProfileId",
                table: "Favorites");

            migrationBuilder.AlterColumn<int>(
                name: "ProfileId",
                table: "Watched",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProfileId",
                table: "Ratings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProfileId",
                table: "RatingEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProfileId",
                table: "Favorites",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Watched",
                table: "Watched",
                columns: new[] { "ProfileId", "TitleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ratings",
                table: "Ratings",
                columns: new[] { "ProfileId", "TitleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Favorites",
                table: "Favorites",
                columns: new[] { "ProfileId", "TitleId" });

            migrationBuilder.CreateIndex(
                name: "IX_Watched_TitleId",
                table: "Watched",
                column: "TitleId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_TitleId",
                table: "Ratings",
                column: "TitleId");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_TitleId",
                table: "Favorites",
                column: "TitleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Favorites_Profiles_ProfileId",
                table: "Favorites",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RatingEvents_Profiles_ProfileId",
                table: "RatingEvents",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ratings_Profiles_ProfileId",
                table: "Ratings",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Watched_Profiles_ProfileId",
                table: "Watched",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "FK_Watched_Profiles_ProfileId",
                table: "Watched");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Watched",
                table: "Watched");

            migrationBuilder.DropIndex(
                name: "IX_Watched_TitleId",
                table: "Watched");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Ratings",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_Ratings_TitleId",
                table: "Ratings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Favorites",
                table: "Favorites");

            migrationBuilder.DropIndex(
                name: "IX_Favorites_TitleId",
                table: "Favorites");

            migrationBuilder.AlterColumn<int>(
                name: "ProfileId",
                table: "Watched",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ProfileId",
                table: "Ratings",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ProfileId",
                table: "RatingEvents",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ProfileId",
                table: "Favorites",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Watched",
                table: "Watched",
                column: "TitleId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Ratings",
                table: "Ratings",
                column: "TitleId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Favorites",
                table: "Favorites",
                column: "TitleId");

            migrationBuilder.CreateIndex(
                name: "IX_Watched_ProfileId",
                table: "Watched",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_ProfileId",
                table: "Ratings",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_ProfileId",
                table: "Favorites",
                column: "ProfileId");

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
                name: "FK_Watched_Profiles_ProfileId",
                table: "Watched",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
