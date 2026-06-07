using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferVarianceAndShipmentConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_goods_receipts_PurchaseOrderShipments_PurchaseOrderShipment~",
                table: "goods_receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderShipmentItems_purchase_order_items_PurchaseOrd~",
                table: "PurchaseOrderShipmentItems");

            migrationBuilder.CreateSequence<int>(
                name: "seq_shipments");

            migrationBuilder.AddColumn<int>(
                name: "VarianceResolutionStatus",
                table: "warehouse_transfers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VarianceResolvedAt",
                table: "warehouse_transfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceId",
                table: "stock_adjustments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferenceType",
                table: "stock_adjustments",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TrackingNumber",
                table: "PurchaseOrderShipments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SupplierNotes",
                table: "PurchaseOrderShipments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ShipmentNumber",
                table: "PurchaseOrderShipments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValueSql: "CONCAT('SHP-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_shipments')::text, 5, '0'))",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "PurchaseOrderShipments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "CarrierName",
                table: "PurchaseOrderShipments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "PurchaseOrderShipmentItems",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

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
                name: "IX_stock_adjustments_ReferenceType_ReferenceId",
                table: "stock_adjustments",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderShipments_ShipmentNumber",
                table: "PurchaseOrderShipments",
                column: "ShipmentNumber",
                unique: true);

            // Backfill transfer variance linkage for existing LossInTransit adjustments
            migrationBuilder.Sql("""
                UPDATE stock_adjustments sa
                SET reference_type = 1, reference_id = wt."Id"
                FROM warehouse_transfers wt
                WHERE sa.reason = 8
                  AND sa.reference_id IS NULL
                  AND sa.notes LIKE 'Transit Variance on Transfer ' || wt."TransferNumber" || '%';
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_goods_receipts_PurchaseOrderShipments_PurchaseOrderShipment~",
                table: "goods_receipts",
                column: "PurchaseOrderShipmentId",
                principalTable: "PurchaseOrderShipments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderShipmentItems_purchase_order_items_PurchaseOrd~",
                table: "PurchaseOrderShipmentItems",
                column: "PurchaseOrderItemId",
                principalTable: "purchase_order_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_goods_receipts_PurchaseOrderShipments_PurchaseOrderShipment~",
                table: "goods_receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderShipmentItems_purchase_order_items_PurchaseOrd~",
                table: "PurchaseOrderShipmentItems");

            migrationBuilder.DropIndex(
                name: "IX_stock_adjustments_ReferenceType_ReferenceId",
                table: "stock_adjustments");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderShipments_ShipmentNumber",
                table: "PurchaseOrderShipments");

            migrationBuilder.DropColumn(
                name: "VarianceResolutionStatus",
                table: "warehouse_transfers");

            migrationBuilder.DropColumn(
                name: "VarianceResolvedAt",
                table: "warehouse_transfers");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "stock_adjustments");

            migrationBuilder.DropColumn(
                name: "ReferenceType",
                table: "stock_adjustments");

            migrationBuilder.DropSequence(
                name: "seq_shipments");

            migrationBuilder.AlterColumn<string>(
                name: "TrackingNumber",
                table: "PurchaseOrderShipments",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SupplierNotes",
                table: "PurchaseOrderShipments",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ShipmentNumber",
                table: "PurchaseOrderShipments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValueSql: "CONCAT('SHP-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_shipments')::text, 5, '0'))");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "PurchaseOrderShipments",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");

            migrationBuilder.AlterColumn<string>(
                name: "CarrierName",
                table: "PurchaseOrderShipments",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "PurchaseOrderShipmentItems",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");

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

            migrationBuilder.AddForeignKey(
                name: "FK_goods_receipts_PurchaseOrderShipments_PurchaseOrderShipment~",
                table: "goods_receipts",
                column: "PurchaseOrderShipmentId",
                principalTable: "PurchaseOrderShipments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderShipmentItems_purchase_order_items_PurchaseOrd~",
                table: "PurchaseOrderShipmentItems",
                column: "PurchaseOrderItemId",
                principalTable: "purchase_order_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
