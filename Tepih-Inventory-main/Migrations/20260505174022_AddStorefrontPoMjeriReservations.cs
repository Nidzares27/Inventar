using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventar.Migrations
{
    /// <inheritdoc />
    public partial class AddStorefrontPoMjeriReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CutLength",
                schema: "commerce",
                table: "InventoryReservations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CutWidth",
                schema: "commerce",
                table: "InventoryReservations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConsumedLengthPerUnit",
                schema: "commerce",
                table: "InventoryReservations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WebOrderItemId",
                schema: "commerce",
                table: "InventoryReservations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PoMjeri",
                schema: "commerce",
                table: "WebOrderItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_WebOrderItemId",
                schema: "commerce",
                table: "InventoryReservations",
                column: "WebOrderItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryReservations_WebOrderItems_WebOrderItemId",
                schema: "commerce",
                table: "InventoryReservations",
                column: "WebOrderItemId",
                principalSchema: "commerce",
                principalTable: "WebOrderItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryReservations_WebOrderItems_WebOrderItemId",
                schema: "commerce",
                table: "InventoryReservations");

            migrationBuilder.DropIndex(
                name: "IX_InventoryReservations_WebOrderItemId",
                schema: "commerce",
                table: "InventoryReservations");

            migrationBuilder.DropColumn(
                name: "CutLength",
                schema: "commerce",
                table: "InventoryReservations");

            migrationBuilder.DropColumn(
                name: "CutWidth",
                schema: "commerce",
                table: "InventoryReservations");

            migrationBuilder.DropColumn(
                name: "ConsumedLengthPerUnit",
                schema: "commerce",
                table: "InventoryReservations");

            migrationBuilder.DropColumn(
                name: "WebOrderItemId",
                schema: "commerce",
                table: "InventoryReservations");

            migrationBuilder.DropColumn(
                name: "PoMjeri",
                schema: "commerce",
                table: "WebOrderItems");
        }
    }
}
