using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventar.Migrations
{
    /// <inheritdoc />
    public partial class AddStorefrontProductFieldsAndCommerceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "commerce");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Tepisi",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OnlinePrice",
                table: "Tepisi",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReservedQuantity",
                table: "Tepisi",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Tepisi",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeoDescription",
                table: "Tepisi",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeoTitle",
                table: "Tepisi",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortDescription",
                table: "Tepisi",
                type: "nvarchar(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Tepisi",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductImages",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TepihId = table.Column<int>(type: "int", nullable: false),
                    CloudinaryPublicId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AltText = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Disabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductImages_Tepisi_TepihId",
                        column: x => x.TepihId,
                        principalTable: "Tepisi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebOrders",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CustomerFirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerLastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    CustomerPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ShippingAddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShippingAddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ShippingCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ShippingPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ShippingCountry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BillingAddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BillingAddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BillingCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BillingPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BillingCountry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ItemsTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ShippingTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FulfillmentStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PaymentProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    PaidUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InternalNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryReservations",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WebOrderId = table.Column<int>(type: "int", nullable: false),
                    TepihId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleasedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryReservations_Tepisi_TepihId",
                        column: x => x.TepihId,
                        principalTable: "Tepisi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReservations_WebOrders_WebOrderId",
                        column: x => x.WebOrderId,
                        principalSchema: "commerce",
                        principalTable: "WebOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebOrderItems",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WebOrderId = table.Column<int>(type: "int", nullable: false),
                    TepihId = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Length = table.Column<int>(type: "int", nullable: true),
                    Width = table.Column<int>(type: "int", nullable: true),
                    PerM2 = table.Column<bool>(type: "bit", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrimaryImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebOrderItems_Tepisi_TepihId",
                        column: x => x.TepihId,
                        principalTable: "Tepisi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WebOrderItems_WebOrders_WebOrderId",
                        column: x => x.WebOrderId,
                        principalSchema: "commerce",
                        principalTable: "WebOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebOrderStatusHistory",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WebOrderId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebOrderStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebOrderStatusHistory_WebOrders_WebOrderId",
                        column: x => x.WebOrderId,
                        principalSchema: "commerce",
                        principalTable: "WebOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tepisi_Slug",
                table: "Tepisi",
                column: "Slug",
                unique: true,
                filter: "[Slug] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_TepihId_Status",
                schema: "commerce",
                table: "InventoryReservations",
                columns: new[] { "TepihId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_WebOrderId_Status",
                schema: "commerce",
                table: "InventoryReservations",
                columns: new[] { "WebOrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_TepihId_IsPrimary",
                schema: "commerce",
                table: "ProductImages",
                columns: new[] { "TepihId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_TepihId_SortOrder",
                schema: "commerce",
                table: "ProductImages",
                columns: new[] { "TepihId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_WebOrderItems_TepihId",
                schema: "commerce",
                table: "WebOrderItems",
                column: "TepihId");

            migrationBuilder.CreateIndex(
                name: "IX_WebOrderItems_WebOrderId",
                schema: "commerce",
                table: "WebOrderItems",
                column: "WebOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WebOrders_OrderNumber",
                schema: "commerce",
                table: "WebOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebOrders_Status_CreatedUtc",
                schema: "commerce",
                table: "WebOrders",
                columns: new[] { "Status", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WebOrderStatusHistory_WebOrderId_ChangedUtc",
                schema: "commerce",
                table: "WebOrderStatusHistory",
                columns: new[] { "WebOrderId", "ChangedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryReservations",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "ProductImages",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "WebOrderItems",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "WebOrderStatusHistory",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "WebOrders",
                schema: "commerce");

            migrationBuilder.DropIndex(
                name: "IX_Tepisi_Slug",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "OnlinePrice",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "ReservedQuantity",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "SeoDescription",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "SeoTitle",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "ShortDescription",
                table: "Tepisi");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Tepisi");
        }
    }
}
