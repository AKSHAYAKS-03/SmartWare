using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class StandardizeIdGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.CreateSequence<int>(name: "seq_suppliers");
            migrationBuilder.CreateSequence<int>(name: "seq_purchase_orders");
            migrationBuilder.CreateSequence<int>(name: "seq_goods_receipts");
            migrationBuilder.CreateSequence<int>(name: "seq_transfers");
            migrationBuilder.CreateSequence<int>(name: "seq_adjustments");



            migrationBuilder.Sql(@"
                ALTER TABLE purchase_orders ALTER COLUMN ""PoNumber"" DROP DEFAULT;
                ALTER TABLE purchase_orders ALTER COLUMN ""PoNumber"" SET DEFAULT CONCAT('PO-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_purchase_orders')::text, 5, '0'));
                
                ALTER TABLE warehouse_transfers ALTER COLUMN ""TransferNumber"" DROP DEFAULT;
                ALTER TABLE warehouse_transfers ALTER COLUMN ""TransferNumber"" SET DEFAULT CONCAT('TRF-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_transfers')::text, 5, '0'));
                
                ALTER TABLE stock_adjustments ALTER COLUMN ""AdjustmentNumber"" DROP DEFAULT;
                ALTER TABLE stock_adjustments ALTER COLUMN ""AdjustmentNumber"" SET DEFAULT CONCAT('ADJ-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_adjustments')::text, 5, '0'));
                
                ALTER TABLE suppliers ALTER COLUMN ""Code"" DROP DEFAULT;
                ALTER TABLE suppliers ALTER COLUMN ""Code"" SET DEFAULT CONCAT('SUP-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_suppliers')::text, 5, '0'));

                ALTER TABLE goods_receipts ALTER COLUMN ""GrnNumber"" DROP DEFAULT;
                ALTER TABLE goods_receipts ALTER COLUMN ""GrnNumber"" SET DEFAULT CONCAT('GRN-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_goods_receipts')::text, 5, '0'));
            ");
            migrationBuilder.Sql(@"
                DO $BLOCK$ 
                DECLARE
                    curr_val INTEGER;
                BEGIN
                    -- Suppliers
                    SELECT MAX(CAST(REGEXP_REPLACE(""Code"", '^SUP-[0-9]{4}-', '') AS INTEGER)) INTO curr_val 
                    FROM suppliers WHERE ""Code"" ~ '^SUP-[0-9]{4}-[0-9]+$';
                    IF curr_val IS NOT NULL THEN
                        PERFORM setval('seq_suppliers', curr_val);
                    END IF;

                    -- Purchase Orders
                    SELECT MAX(CAST(REGEXP_REPLACE(""PoNumber"", '^PO-[0-9]{4}-', '') AS INTEGER)) INTO curr_val 
                    FROM purchase_orders WHERE ""PoNumber"" ~ '^PO-[0-9]{4}-[0-9]+$';
                    IF curr_val IS NOT NULL THEN
                        PERFORM setval('seq_purchase_orders', curr_val);
                    END IF;

                    -- Goods Receipts
                    SELECT MAX(CAST(REGEXP_REPLACE(""GrnNumber"", '^GRN-[0-9]{4}-', '') AS INTEGER)) INTO curr_val 
                    FROM goods_receipts WHERE ""GrnNumber"" ~ '^GRN-[0-9]{4}-[0-9]+$';
                    IF curr_val IS NOT NULL THEN
                        PERFORM setval('seq_goods_receipts', curr_val);
                    END IF;

                    -- Warehouse Transfers
                    SELECT MAX(CAST(REGEXP_REPLACE(""TransferNumber"", '^TRF-[0-9]{4}-', '') AS INTEGER)) INTO curr_val 
                    FROM warehouse_transfers WHERE ""TransferNumber"" ~ '^TRF-[0-9]{4}-[0-9]+$';
                    IF curr_val IS NOT NULL THEN
                        PERFORM setval('seq_transfers', curr_val);
                    END IF;

                    -- Stock Adjustments
                    SELECT MAX(CAST(REGEXP_REPLACE(""AdjustmentNumber"", '^ADJ-[0-9]{4}-', '') AS INTEGER)) INTO curr_val 
                    FROM stock_adjustments WHERE ""AdjustmentNumber"" ~ '^ADJ-[0-9]{4}-[0-9]+$';
                    IF curr_val IS NOT NULL THEN
                        PERFORM setval('seq_adjustments', curr_val);
                    END IF;
                END $BLOCK$;
            ");

            
            migrationBuilder.Sql(@"
                DO $BLOCK$ 
                DECLARE
                    curr_val INTEGER;
                BEGIN
                    -- Suppliers
                    SELECT MAX(CAST(REGEXP_REPLACE(""Code"", '^SUP-[0-9]{4}-', '') AS INTEGER)) INTO curr_val 
                    FROM suppliers WHERE ""Code"" ~ '^SUP-[0-9]{4}-[0-9]+$';
                    IF curr_val IS NOT NULL THEN
                        PERFORM setval('seq_suppliers', curr_val);
                    END IF;

                    -- Purchase Orders
                    SELECT MAX(CAST(REGEXP_REPLACE(""PoNumber"", '^PO-[0-9]{4}-', '') AS INTEGER)) INTO curr_val 
                    FROM purchase_orders WHERE ""PoNumber"" ~ '^PO-[0-9]{4}-[0-9]+$';
                    IF curr_val IS NOT NULL THEN
                        PERFORM setval('seq_purchase_orders', curr_val);
                    END IF;

                    -- Goods Receipts
                    SELECT MAX(CAST(REGEXP_REPLACE(""GrnNumber"", '^GRN-[0-9]{4}-', '') AS INTEGER)) INTO curr_val 
                    FROM goods_receipts WHERE ""GrnNumber"" ~ '^GRN-[0-9]{4}-[0-9]+$';
                    IF curr_val IS NOT NULL THEN
                        PERFORM setval('seq_goods_receipts', curr_val);
                    END IF;

                    -- Warehouse Transfers
                    SELECT MAX(CAST(REGEXP_REPLACE(""TransferNumber"", '^TRF-[0-9]{4}-', '') AS INTEGER)) INTO curr_val 
                    FROM warehouse_transfers WHERE ""TransferNumber"" ~ '^TRF-[0-9]{4}-[0-9]+$';
                    IF curr_val IS NOT NULL THEN
                        PERFORM setval('seq_transfers', curr_val);
                    END IF;

                    -- Stock Adjustments
                    SELECT MAX(CAST(REGEXP_REPLACE(""AdjustmentNumber"", '^ADJ-[0-9]{4}-', '') AS INTEGER)) INTO curr_val 
                    FROM stock_adjustments WHERE ""AdjustmentNumber"" ~ '^ADJ-[0-9]{4}-[0-9]+$';
                    IF curr_val IS NOT NULL THEN
                        PERFORM setval('seq_adjustments', curr_val);
                    END IF;
                END $BLOCK$;
            ");
            
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS trg_set_po_number ON purchase_orders;
                DROP TRIGGER IF EXISTS trg_set_grn_number ON goods_receipts;
                DROP TRIGGER IF EXISTS trg_set_transfer_number ON warehouse_transfers;
                DROP TRIGGER IF EXISTS trg_set_adjustment_number ON stock_adjustments;
                DROP TRIGGER IF EXISTS trg_set_supplier_code ON suppliers;

                DROP FUNCTION IF EXISTS trg_fn_set_po_number();
                DROP FUNCTION IF EXISTS trg_fn_set_grn_number();
                DROP FUNCTION IF EXISTS trg_fn_set_transfer_number();
                DROP FUNCTION IF EXISTS trg_fn_set_adjustment_number();
                DROP FUNCTION IF EXISTS trg_fn_set_supplier_code();
                DROP FUNCTION IF EXISTS fn_generate_sequence_number(VARCHAR);

                DROP TABLE IF EXISTS sequence_counters;
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

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS sequence_counters (
                    ""Id"" uuid NOT NULL,
                    ""EntityName"" character varying(50) NOT NULL,
                    ""Prefix"" character varying(10) NOT NULL,
                    ""CurrentValue"" integer NOT NULL,
                    ""LastUpdated"" timestamp with time zone NOT NULL,
                    CONSTRAINT PK_sequence_counters PRIMARY KEY (""Id"")
                );

                INSERT INTO sequence_counters (""Id"", ""EntityName"", ""Prefix"", ""CurrentValue"", ""LastUpdated"")
                VALUES 
                (gen_random_uuid(), 'PurchaseOrder', 'PO', COALESCE((SELECT last_value FROM seq_purchase_orders), 0), NOW()),
                (gen_random_uuid(), 'GoodsReceipt', 'GRN', COALESCE((SELECT last_value FROM seq_goods_receipts), 0), NOW()),
                (gen_random_uuid(), 'WarehouseTransfer', 'TRF', COALESCE((SELECT last_value FROM seq_transfers), 0), NOW()),
                (gen_random_uuid(), 'StockAdjustment', 'ADJ', COALESCE((SELECT last_value FROM seq_adjustments), 0), NOW()),
                (gen_random_uuid(), 'Supplier', 'SUP', COALESCE((SELECT last_value FROM seq_suppliers), 0), NOW());
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
