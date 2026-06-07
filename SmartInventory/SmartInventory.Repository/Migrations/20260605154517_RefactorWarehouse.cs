using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class RefactorWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_warehouses_TaxIdentifier",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "EncryptedRegistrationNumber",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "EncryptedTaxIdentifier",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "RegistrationNumberLastFour",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "ShareContactDetails",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "TaxIdentifierLastFour",
                table: "warehouses");

            migrationBuilder.RenameColumn(
                name: "TaxIdentifier",
                table: "warehouses",
                newName: "ContactNumber");

            migrationBuilder.AddColumn<string>(
                name: "ContactPerson",
                table: "warehouses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "warehouses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GSTIN",
                table: "warehouses",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "warehouses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "warehouses",
                type: "character varying(100)",
                maxLength: 100,
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
                name: "IX_warehouses_GSTIN",
                table: "warehouses",
                column: "GSTIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_warehouses_GSTIN",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "ContactPerson",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "GSTIN",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "State",
                table: "warehouses");

            migrationBuilder.RenameColumn(
                name: "ContactNumber",
                table: "warehouses",
                newName: "TaxIdentifier");

            migrationBuilder.AddColumn<string>(
                name: "EncryptedRegistrationNumber",
                table: "warehouses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedTaxIdentifier",
                table: "warehouses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNumberLastFour",
                table: "warehouses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShareContactDetails",
                table: "warehouses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TaxIdentifierLastFour",
                table: "warehouses",
                type: "text",
                nullable: true);

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
                name: "IX_warehouses_TaxIdentifier",
                table: "warehouses",
                column: "TaxIdentifier",
                unique: true,
                filter: "\"TaxIdentifier\" IS NOT NULL");
        }
    }
}
