using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventar.Migrations
{
    public partial class SalesForeignKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Prodaje_TepihId",
                table: "Prodaje",
                column: "TepihId");

            migrationBuilder.AddForeignKey(
                name: "FK_Prodaje_Tepisi_TepihId",
                table: "Prodaje",
                column: "TepihId",
                principalTable: "Tepisi",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Prodaje_Tepisi_TepihId",
                table: "Prodaje");

            migrationBuilder.DropIndex(
                name: "IX_Prodaje_TepihId",
                table: "Prodaje");
        }
    }
}
