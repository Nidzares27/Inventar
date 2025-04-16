using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventar.Migrations
{
    public partial class NewTepihModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "M3",
                table: "Tepisi");

            migrationBuilder.RenameColumn(
                name: "Size",
                table: "Tepisi",
                newName: "PriceUnit");

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "Tepisi",
                newName: "PriceM2");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Tepisi",
                newName: "ProductNumber");

            migrationBuilder.AddColumn<int>(
                name: "Length",
                table: "Tepisi",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "Tepisi",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "Tepisi",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Length",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "Tepisi");

            migrationBuilder.RenameColumn(
                name: "ProductNumber",
                table: "Tepisi",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "PriceUnit",
                table: "Tepisi",
                newName: "Size");

            migrationBuilder.RenameColumn(
                name: "PriceM2",
                table: "Tepisi",
                newName: "Price");

            migrationBuilder.AddColumn<decimal>(
                name: "M3",
                table: "Tepisi",
                type: "decimal(18,2)",
                nullable: true);
        }
    }
}
