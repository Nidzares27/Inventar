using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventar.Migrations
{
    public partial class DodatDisabled : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Disabled",
                table: "Tepisi",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Disabled",
                table: "Prodaje",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Disabled",
                table: "Placanja",
                type: "bit",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Disabled",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "Disabled",
                table: "Prodaje");

            migrationBuilder.DropColumn(
                name: "Disabled",
                table: "Placanja");
        }
    }
}
