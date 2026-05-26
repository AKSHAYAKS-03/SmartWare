using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddTriggersAndFunctions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop existing functions/triggers if any exist (safety)
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_set_adjustment_number ON stock_adjustments;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_set_po_number ON purchase_orders;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_set_grn_number ON goods_receipts;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_set_transfer_number ON warehouse_transfers;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_process_stock_adjustment ON stock_adjustments;");
            
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS trg_fn_set_adjustment_number();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS trg_fn_set_po_number();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS trg_fn_set_grn_number();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS trg_fn_set_transfer_number();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS trg_fn_process_stock_adjustment();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_generate_sequence_number(VARCHAR);");

            // 2. Create the Sequence Number Generator Function
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION fn_generate_sequence_number(entity_type VARCHAR)
                RETURNS VARCHAR AS $$
                DECLARE
                    seq_prefix VARCHAR;
                    seq_val INT;
                    year_val VARCHAR;
                BEGIN
                    -- Find the counter and lock it
                    SELECT ""Prefix"", ""CurrentValue"" + 1 INTO seq_prefix, seq_val
                    FROM sequence_counters
                    WHERE ""EntityName"" = entity_type
                    FOR UPDATE;

                    IF NOT FOUND THEN
                        RAISE EXCEPTION 'Sequence counter for % not found.', entity_type;
                    END IF;

                    -- Update the counter
                    UPDATE sequence_counters
                    SET ""CurrentValue"" = seq_val
                    WHERE ""EntityName"" = entity_type;

                    -- Format suffix (e.g. PO-2026-00042)
                    year_val := to_char(CURRENT_DATE, 'YYYY');
                    RETURN seq_prefix || '-' || year_val || '-' || lpad(seq_val::VARCHAR, 5, '0');
                END;
                $$ LANGUAGE plpgsql;
            ");

            // 3. Create Trigger Function for Stock Adjustment Number
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION trg_fn_set_adjustment_number()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF NEW.""AdjustmentNumber"" IS NULL OR NEW.""AdjustmentNumber"" = '' OR NEW.""AdjustmentNumber"" = 'TEMP' THEN
                        NEW.""AdjustmentNumber"" := fn_generate_sequence_number('StockAdjustment');
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_set_adjustment_number
                BEFORE INSERT ON stock_adjustments
                FOR EACH ROW
                EXECUTE FUNCTION trg_fn_set_adjustment_number();
            ");

            // 4. Create Trigger Function for Purchase Order Number
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION trg_fn_set_po_number()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF NEW.""PoNumber"" IS NULL OR NEW.""PoNumber"" = '' OR NEW.""PoNumber"" = 'TEMP' THEN
                        NEW.""PoNumber"" := fn_generate_sequence_number('PurchaseOrder');
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_set_po_number
                BEFORE INSERT ON purchase_orders
                FOR EACH ROW
                EXECUTE FUNCTION trg_fn_set_po_number();
            ");

            // 5. Create Trigger Function for Goods Receipt Number
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION trg_fn_set_grn_number()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF NEW.""GrnNumber"" IS NULL OR NEW.""GrnNumber"" = '' OR NEW.""GrnNumber"" = 'TEMP' THEN
                        NEW.""GrnNumber"" := fn_generate_sequence_number('GoodsReceipt');
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_set_grn_number
                BEFORE INSERT ON goods_receipts
                FOR EACH ROW
                EXECUTE FUNCTION trg_fn_set_grn_number();
            ");

            // 6. Create Trigger Function for Warehouse Transfer Number
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION trg_fn_set_transfer_number()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF NEW.""TransferNumber"" IS NULL OR NEW.""TransferNumber"" = '' OR NEW.""TransferNumber"" = 'TEMP' THEN
                        NEW.""TransferNumber"" := fn_generate_sequence_number('WarehouseTransfer');
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_set_transfer_number
                BEFORE INSERT ON warehouse_transfers
                FOR EACH ROW
                EXECUTE FUNCTION trg_fn_set_transfer_number();
            ");

            // 7. Create Trigger Function for Stock Level Processing and Movement Logs on Adjustment Approval
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_set_adjustment_number ON stock_adjustments;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_set_po_number ON purchase_orders;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_set_grn_number ON goods_receipts;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_set_transfer_number ON warehouse_transfers;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_process_stock_adjustment ON stock_adjustments;");
            
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS trg_fn_set_adjustment_number();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS trg_fn_set_po_number();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS trg_fn_set_grn_number();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS trg_fn_set_transfer_number();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS trg_fn_process_stock_adjustment();");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_generate_sequence_number(VARCHAR);");
        }
    }
}
