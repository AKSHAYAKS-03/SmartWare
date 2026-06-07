using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierShipmentEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrderShipmentId",
                table: "goods_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseOrderShipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentNumber = table.Column<string>(type: "text", nullable: false),
                    TrackingNumber = table.Column<string>(type: "text", nullable: true),
                    CarrierName = table.Column<string>(type: "text", nullable: true),
                    DispatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedDelivery = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SupplierNotes = table.Column<string>(type: "text", nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderShipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderShipments_purchase_orders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "purchase_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderShipmentItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityDispatched = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderShipmentItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderShipmentItems_PurchaseOrderShipments_PurchaseO~",
                        column: x => x.PurchaseOrderShipmentId,
                        principalTable: "PurchaseOrderShipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderShipmentItems_purchase_order_items_PurchaseOrd~",
                        column: x => x.PurchaseOrderItemId,
                        principalTable: "purchase_order_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("a0d33b91-4567-4eef-b123-999999999901"),
                column: "Permissions",
                value: new List<string> { "Admin", "Manage", "Inventory", "View" });

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("a0d33b91-4567-4eef-b123-999999999902"),
                column: "Permissions",
                value: new List<string> { "Manage", "Inventory", "View" });

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("a0d33b91-4567-4eef-b123-999999999903"),
                column: "Permissions",
                value: new List<string> { "Inventory", "View" });

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("a0d33b91-4567-4eef-b123-999999999904"),
                column: "Permissions",
                value: new List<string> { "View" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_PurchaseOrderShipmentId",
                table: "goods_receipts",
                column: "PurchaseOrderShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderShipmentItems_PurchaseOrderItemId",
                table: "PurchaseOrderShipmentItems",
                column: "PurchaseOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderShipmentItems_PurchaseOrderShipmentId",
                table: "PurchaseOrderShipmentItems",
                column: "PurchaseOrderShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderShipments_PurchaseOrderId",
                table: "PurchaseOrderShipments",
                column: "PurchaseOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_goods_receipts_PurchaseOrderShipments_PurchaseOrderShipment~",
                table: "goods_receipts",
                column: "PurchaseOrderShipmentId",
                principalTable: "PurchaseOrderShipments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_goods_receipts_PurchaseOrderShipments_PurchaseOrderShipment~",
                table: "goods_receipts");

            migrationBuilder.DropTable(
                name: "PurchaseOrderShipmentItems");

            migrationBuilder.DropTable(
                name: "PurchaseOrderShipments");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipts_PurchaseOrderShipmentId",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderShipmentId",
                table: "goods_receipts");

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("a0d33b91-4567-4eef-b123-999999999901"),
                column: "Permissions",
                value: new List<string> { "Admin", "Manage", "Inventory", "View" });

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("a0d33b91-4567-4eef-b123-999999999902"),
                column: "Permissions",
                value: new List<string> { "Manage", "Inventory", "View" });

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("a0d33b91-4567-4eef-b123-999999999903"),
                column: "Permissions",
                value: new List<string> { "Inventory", "View" });

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("a0d33b91-4567-4eef-b123-999999999904"),
                column: "Permissions",
                value: new List<string> { "View" });
        }
    }
}
