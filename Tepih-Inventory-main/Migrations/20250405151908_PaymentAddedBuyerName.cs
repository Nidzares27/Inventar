using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventar.Migrations
{
    public partial class PaymentAddedBuyerName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "Placanja",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "Placanja");
        }
    }
}
