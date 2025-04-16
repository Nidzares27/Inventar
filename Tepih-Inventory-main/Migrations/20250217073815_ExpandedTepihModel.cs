using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventar.Migrations
{
    public partial class ExpandedTepihModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "Tepisi",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "M3",
                table: "Tepisi",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Tepisi",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Size",
                table: "Tepisi",
                type: "decimal(18,2)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Color",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "M3",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "Tepisi");
        }
    }
}
