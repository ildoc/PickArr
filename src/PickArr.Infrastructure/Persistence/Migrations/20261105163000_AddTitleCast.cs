using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PickArr.Infrastructure.Persistence;

#nullable disable

namespace PickArr.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(PickArrDbContext))]
    [Migration("20261105163000_AddTitleCast")]
    public partial class AddTitleCast : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CastJson",
                table: "Titles",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CastJson",
                table: "Titles");
        }
    }
}
