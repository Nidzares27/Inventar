using Inventar.Data;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Inventar.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260504170000_AddPoMjeriProducts")]
    public partial class AddPoMjeriProducts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsumedLength",
                table: "Prodaje",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomLength",
                table: "Prodaje",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomWidth",
                table: "Prodaje",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PoMjeri",
                table: "Tepisi",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UnID",
                table: "Tepisi",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tepisi_UnID",
                table: "Tepisi",
                column: "UnID",
                unique: true,
                filter: "[UnID] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tepisi_UnID",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "ConsumedLength",
                table: "Prodaje");

            migrationBuilder.DropColumn(
                name: "CustomLength",
                table: "Prodaje");

            migrationBuilder.DropColumn(
                name: "CustomWidth",
                table: "Prodaje");

            migrationBuilder.DropColumn(
                name: "PoMjeri",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "UnID",
                table: "Tepisi");
        }
    }
}
