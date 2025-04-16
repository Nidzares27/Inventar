using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventar.Migrations
{
    public partial class Payment : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LeftToPay",
                table: "Kupci",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Placanja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Placanja", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Placanja");

            migrationBuilder.DropColumn(
                name: "LeftToPay",
                table: "Kupci");
        }
    }
}
