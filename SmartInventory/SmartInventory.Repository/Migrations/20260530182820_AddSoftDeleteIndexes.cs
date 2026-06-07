using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_warehouses_IsActive",
                table: "warehouses",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_zones_IsActive",
                table: "warehouse_zones",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_users_IsActive",
                table: "users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_IsActive",
                table: "suppliers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_products_IsActive",
                table: "products",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_IsActive",
                table: "product_variants",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_categories_IsActive",
                table: "categories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_bin_locations_IsActive",
                table: "bin_locations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_alert_configurations_IsActive",
                table: "alert_configurations",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_warehouses_IsActive",
                table: "warehouses");

            migrationBuilder.DropIndex(
                name: "IX_warehouse_zones_IsActive",
                table: "warehouse_zones");

            migrationBuilder.DropIndex(
                name: "IX_users_IsActive",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_suppliers_IsActive",
                table: "suppliers");

            migrationBuilder.DropIndex(
                name: "IX_products_IsActive",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_product_variants_IsActive",
                table: "product_variants");

            migrationBuilder.DropIndex(
                name: "IX_categories_IsActive",
                table: "categories");

            migrationBuilder.DropIndex(
                name: "IX_bin_locations_IsActive",
                table: "bin_locations");

            migrationBuilder.DropIndex(
                name: "IX_alert_configurations_IsActive",
                table: "alert_configurations");
        }
    }
}
