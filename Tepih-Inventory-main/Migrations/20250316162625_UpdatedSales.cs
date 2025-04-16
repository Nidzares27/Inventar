using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventar.Migrations
{
    public partial class UpdatedSales : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PriceM2",
                table: "Prodaje",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceUnit",
                table: "Prodaje",
                type: "decimal(18,2)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PriceM2",
                table: "Prodaje");

            migrationBuilder.DropColumn(
                name: "PriceUnit",
                table: "Prodaje");
        }
    }
}
