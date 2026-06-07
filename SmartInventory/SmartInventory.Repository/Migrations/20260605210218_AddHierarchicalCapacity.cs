using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddHierarchicalCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AreaSqFt",
                table: "warehouses",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxVolumeCm3",
                table: "warehouses",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxWeightKg",
                table: "warehouses",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AreaSqFt",
                table: "warehouse_zones",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxVolumeCm3",
                table: "warehouse_zones",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxWeightKg",
                table: "warehouse_zones",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            // Backfill Data from Bins -> Zones
            migrationBuilder.Sql(@"
                UPDATE warehouse_zones z
                SET ""MaxVolumeCm3"" = COALESCE((SELECT SUM(""MaxVolumeCm3"") FROM bin_locations b WHERE b.""ZoneId"" = z.""Id""), 0),
                    ""MaxWeightKg""  = COALESCE((SELECT SUM(""MaxWeightKg"") FROM bin_locations b WHERE b.""ZoneId"" = z.""Id""), 0);
            ");

            // Backfill Data from Zones -> Warehouses
            migrationBuilder.Sql(@"
                UPDATE warehouses w
                SET ""MaxVolumeCm3"" = COALESCE((SELECT SUM(""MaxVolumeCm3"") FROM warehouse_zones z WHERE z.""WarehouseId"" = w.""Id""), 0),
                    ""MaxWeightKg""  = COALESCE((SELECT SUM(""MaxWeightKg"") FROM warehouse_zones z WHERE z.""WarehouseId"" = w.""Id""), 0);
            ");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AreaSqFt",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "MaxVolumeCm3",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "MaxWeightKg",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "AreaSqFt",
                table: "warehouse_zones");

            migrationBuilder.DropColumn(
                name: "MaxVolumeCm3",
                table: "warehouse_zones");

            migrationBuilder.DropColumn(
                name: "MaxWeightKg",
                table: "warehouse_zones");

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
