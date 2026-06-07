using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseAuditRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "warehouses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "warehouse_zones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "warehouse_transfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "user_warehouse_access",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "transfer_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "suppliers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "SupplierRefreshTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedAmount",
                table: "SupplierInvoices",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "SupplierInvoices",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "SupplierInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "SupplierInvoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "SupplierContacts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "supplier_products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "supplier_performance_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "stock_movements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "stock_levels",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "stock_adjustments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "sequence_counters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "refresh_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "purchase_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "purchase_order_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "product_variants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "notification_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "goods_receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "goods_receipt_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "file_attachments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "categories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "bin_locations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "barcodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "barcode_scan_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "AuditLogArchives",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AuditLogArchives",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "audit_logs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "audit_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "alert_configurations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("a0d33b91-4567-4eef-b123-999999999901"),
                column: "UpdatedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("a0d33b91-4567-4eef-b123-999999999902"),
                column: "UpdatedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("a0d33b91-4567-4eef-b123-999999999903"),
                column: "UpdatedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "roles",
                keyColumn: "Id",
                keyValue: new Guid("a0d33b91-4567-4eef-b123-999999999904"),
                column: "UpdatedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "sequence_counters",
                keyColumn: "Id",
                keyValue: new Guid("c0d33b91-4567-4eef-b123-777777777701"),
                column: "UpdatedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "sequence_counters",
                keyColumn: "Id",
                keyValue: new Guid("c0d33b91-4567-4eef-b123-777777777702"),
                column: "UpdatedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "sequence_counters",
                keyColumn: "Id",
                keyValue: new Guid("c0d33b91-4567-4eef-b123-777777777703"),
                column: "UpdatedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "sequence_counters",
                keyColumn: "Id",
                keyValue: new Guid("c0d33b91-4567-4eef-b123-777777777704"),
                column: "UpdatedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "sequence_counters",
                keyColumn: "Id",
                keyValue: new Guid("c0d33b91-4567-4eef-b123-777777777705"),
                column: "UpdatedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "sequence_counters",
                keyColumn: "Id",
                keyValue: new Guid("c0d33b91-4567-4eef-b123-777777777706"),
                column: "UpdatedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "sequence_counters",
                keyColumn: "Id",
                keyValue: new Guid("c0d33b91-4567-4eef-b123-777777777707"),
                column: "UpdatedAt",
                value: null);

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("b0d33b91-4567-4eef-b123-888888888801"),
                column: "UpdatedAt",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "warehouse_zones");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "warehouse_transfers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "user_warehouse_access");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "transfer_items");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "SupplierRefreshTokens");

            migrationBuilder.DropColumn(
                name: "ApprovedAmount",
                table: "SupplierInvoices");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "SupplierInvoices");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "SupplierInvoices");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "SupplierInvoices");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "SupplierContacts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "supplier_products");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "supplier_performance_logs");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "stock_levels");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "stock_adjustments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "sequence_counters");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "purchase_order_items");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "products");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "notification_logs");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "goods_receipt_items");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "file_attachments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "bin_locations");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "barcodes");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "barcode_scan_logs");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AuditLogArchives");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "alert_configurations");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "AuditLogArchives",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "audit_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
