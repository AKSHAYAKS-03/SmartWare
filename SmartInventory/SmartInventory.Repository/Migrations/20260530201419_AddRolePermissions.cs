using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "Permissions",
                table: "roles",
                type: "text[]",
                nullable: false, defaultValue: new string[0]);

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
                name: "Permissions",
                table: "roles");
        }
    }
}
