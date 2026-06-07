using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSupplierContactOTP : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "suppliers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "SupplierContacts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastOtpSentAt",
                table: "SupplierContacts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OtpLockedUntil",
                table: "SupplierContacts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OtpMaxResends",
                table: "SupplierContacts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OtpMaxRetries",
                table: "SupplierContacts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OtpResendCount",
                table: "SupplierContacts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OtpRetryCount",
                table: "SupplierContacts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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
                name: "IX_suppliers_GSTIN",
                table: "suppliers",
                column: "GSTIN",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_PAN",
                table: "suppliers",
                column: "PAN",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_suppliers_GSTIN",
                table: "suppliers");

            migrationBuilder.DropIndex(
                name: "IX_suppliers_PAN",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "LastOtpSentAt",
                table: "SupplierContacts");

            migrationBuilder.DropColumn(
                name: "OtpLockedUntil",
                table: "SupplierContacts");

            migrationBuilder.DropColumn(
                name: "OtpMaxResends",
                table: "SupplierContacts");

            migrationBuilder.DropColumn(
                name: "OtpMaxRetries",
                table: "SupplierContacts");

            migrationBuilder.DropColumn(
                name: "OtpResendCount",
                table: "SupplierContacts");

            migrationBuilder.DropColumn(
                name: "OtpRetryCount",
                table: "SupplierContacts");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "suppliers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "SupplierContacts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

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
