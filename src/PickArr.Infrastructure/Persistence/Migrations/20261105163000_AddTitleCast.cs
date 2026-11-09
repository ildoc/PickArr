using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pickarr.Infrastructure.Persistence;

#nullable disable

namespace Pickarr.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(PickarrDbContext))]
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
