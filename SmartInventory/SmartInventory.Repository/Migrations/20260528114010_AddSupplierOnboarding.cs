using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AgreementSignedAt",
                table: "suppliers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgreementSignedIp",
                table: "suppliers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InfoRequestedMessage",
                table: "suppliers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteToken",
                table: "suppliers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InviteTokenExpiresAt",
                table: "suppliers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegistrationSource",
                table: "suppliers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "suppliers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "suppliers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionReason",
                table: "suppliers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "SupplierContacts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "SupplierContacts",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "JobTitle",
                table: "SupplierContacts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "SupplierContacts",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "SupplierContacts",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "SupplierContacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerifyExpiresAt",
                table: "SupplierContacts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailVerifyToken",
                table: "SupplierContacts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Create trigger to set supplier code automatically if null/empty/TEMP on insert
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION trg_fn_set_supplier_code()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF NEW.""Code"" IS NULL OR NEW.""Code"" = '' OR NEW.""Code"" = 'TEMP' THEN
                        NEW.""Code"" := fn_generate_sequence_number('Supplier');
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_set_supplier_code
                BEFORE INSERT ON suppliers
                FOR EACH ROW
                EXECUTE FUNCTION trg_fn_set_supplier_code();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgreementSignedAt",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "AgreementSignedIp",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "InfoRequestedMessage",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "InviteToken",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "InviteTokenExpiresAt",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "RegistrationSource",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "SuspensionReason",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "EmailVerified",
                table: "SupplierContacts");

            migrationBuilder.DropColumn(
                name: "EmailVerifyExpiresAt",
                table: "SupplierContacts");

            migrationBuilder.DropColumn(
                name: "EmailVerifyToken",
                table: "SupplierContacts");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "SupplierContacts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "SupplierContacts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldMaxLength: 250);

            migrationBuilder.AlterColumn<string>(
                name: "JobTitle",
                table: "SupplierContacts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "SupplierContacts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "SupplierContacts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_set_supplier_code ON suppliers;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS trg_fn_set_supplier_code();");
        }
    }
}
