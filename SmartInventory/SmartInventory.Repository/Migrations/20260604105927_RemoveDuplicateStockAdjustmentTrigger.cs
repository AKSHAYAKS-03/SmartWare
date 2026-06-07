using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDuplicateStockAdjustmentTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_process_stock_adjustment ON stock_adjustments;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS trg_fn_process_stock_adjustment();");

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
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION trg_fn_process_stock_adjustment()
                RETURNS TRIGGER AS $$
                DECLARE
                    calculated_change INT;
                BEGIN
                    calculated_change := NEW.""QuantityAfter"" - NEW.""QuantityBefore"";
                    NEW.""QuantityChange"" := calculated_change;

                    -- If the adjustment status is changed to Approved (1)
                    IF NEW.""Status"" = 1 AND (OLD IS NULL OR OLD.""Status"" = 0) THEN
                        -- 1. Ensure stock level record exists
                        INSERT INTO stock_levels (
                            ""Id"", ""ProductId"", ""WarehouseId"", ""BinLocationId"", 
                            ""QuantityOnHand"", ""QuantityReserved"", ""QuantityOnOrder"", ""LastUpdated"", ""CreatedAt""
                        )
                        VALUES (
                            gen_random_uuid(), NEW.""ProductId"", NEW.""WarehouseId"", NEW.""BinLocationId"", 
                            NEW.""QuantityAfter"", 0, 0, NOW(), NOW()
                        )
                        ON CONFLICT (""ProductId"", ""WarehouseId"", ""BinLocationId"") 
                        DO UPDATE SET 
                            ""QuantityOnHand"" = NEW.""QuantityAfter"",
                            ""LastUpdated"" = NOW();

                        -- 2. Log Stock Movement
                        INSERT INTO stock_movements (
                            ""Id"", ""ProductId"", ""WarehouseId"", ""BinLocationId"", 
                            ""MovementType"", ""Quantity"", ""ReferenceType"", ""ReferenceId"", 
                            ""PerformedBy"", ""CreatedAt""
                        )
                        VALUES (
                            gen_random_uuid(), NEW.""ProductId"", NEW.""WarehouseId"", NEW.""BinLocationId"",
                            CASE WHEN calculated_change > 0 THEN 2 ELSE 3 END, -- 2=AdjustmentIn, 3=AdjustmentOut
                            ABS(calculated_change),
                            4, -- ReferenceType.StockAdjustment (Mapped to 4)
                            NEW.""PerformedBy"",
                            NOW()
                        );
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_process_stock_adjustment
                BEFORE INSERT OR UPDATE ON stock_adjustments
                FOR EACH ROW
                EXECUTE FUNCTION trg_fn_process_stock_adjustment();
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
    }
}
