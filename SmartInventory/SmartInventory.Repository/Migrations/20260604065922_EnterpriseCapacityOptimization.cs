using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseCapacityOptimization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCapacityEnforced",
                table: "warehouse_zones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Height",
                table: "products",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Length",
                table: "products",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PreferredBinType",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightKg",
                table: "products",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Width",
                table: "products",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "BinType",
                table: "bin_locations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxVolumeCm3",
                table: "bin_locations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxWeightKg",
                table: "bin_locations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UtilizedVolumeCm3",
                table: "bin_locations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UtilizedWeightKg",
                table: "bin_locations",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "bin_locations",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "OverrideAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RuleBroken = table.Column<string>(type: "text", nullable: false),
                    OverrideReason = table.Column<string>(type: "text", nullable: false),
                    SourceBinId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetBinId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreviousStateJson = table.Column<string>(type: "text", nullable: true),
                    NewStateJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OverrideAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OverrideAuditLogs_bin_locations_TargetBinId",
                        column: x => x.TargetBinId,
                        principalTable: "bin_locations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OverrideAuditLogs_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OverrideAuditLogs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
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
                name: "IX_OverrideAuditLogs_ProductId",
                table: "OverrideAuditLogs",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OverrideAuditLogs_TargetBinId",
                table: "OverrideAuditLogs",
                column: "TargetBinId");

            migrationBuilder.CreateIndex(
                name: "IX_OverrideAuditLogs_UserId",
                table: "OverrideAuditLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OverrideAuditLogs");

            migrationBuilder.DropColumn(
                name: "IsCapacityEnforced",
                table: "warehouse_zones");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "products");

            migrationBuilder.DropColumn(
                name: "Length",
                table: "products");

            migrationBuilder.DropColumn(
                name: "PreferredBinType",
                table: "products");

            migrationBuilder.DropColumn(
                name: "WeightKg",
                table: "products");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "products");

            migrationBuilder.DropColumn(
                name: "BinType",
                table: "bin_locations");

            migrationBuilder.DropColumn(
                name: "MaxVolumeCm3",
                table: "bin_locations");

            migrationBuilder.DropColumn(
                name: "MaxWeightKg",
                table: "bin_locations");

            migrationBuilder.DropColumn(
                name: "UtilizedVolumeCm3",
                table: "bin_locations");

            migrationBuilder.DropColumn(
                name: "UtilizedWeightKg",
                table: "bin_locations");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "bin_locations");

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
