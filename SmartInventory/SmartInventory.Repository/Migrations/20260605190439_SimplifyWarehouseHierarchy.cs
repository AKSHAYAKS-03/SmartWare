using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyWarehouseHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bin_locations_ZoneId_Aisle_Rack_Bin",
                table: "bin_locations");

            migrationBuilder.DropColumn(
                name: "Aisle",
                table: "bin_locations");

            migrationBuilder.DropColumn(
                name: "Bin",
                table: "bin_locations");

            migrationBuilder.DropColumn(
                name: "Rack",
                table: "bin_locations");

            migrationBuilder.AddColumn<string>(
                name: "BinCode",
                table: "bin_locations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

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
                name: "IX_bin_locations_ZoneId_BinCode",
                table: "bin_locations",
                columns: new[] { "ZoneId", "BinCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bin_locations_ZoneId_BinCode",
                table: "bin_locations");

            migrationBuilder.DropColumn(
                name: "BinCode",
                table: "bin_locations");

            migrationBuilder.AddColumn<string>(
                name: "Aisle",
                table: "bin_locations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Bin",
                table: "bin_locations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Rack",
                table: "bin_locations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

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
                name: "IX_bin_locations_ZoneId_Aisle_Rack_Bin",
                table: "bin_locations",
                columns: new[] { "ZoneId", "Aisle", "Rack", "Bin" },
                unique: true);
        }
    }
}
