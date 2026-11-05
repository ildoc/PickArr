using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PickArr.Infrastructure.Persistence;

#nullable disable

namespace PickArr.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(PickArrDbContext))]
    [Migration("20261105170000_AddTitleSeasons")]
    public partial class AddTitleSeasons : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SeasonsJson",
                table: "Titles",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeasonsJson",
                table: "Titles");
        }
    }
}
