using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalPlanFeaturesAndArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<string>(
                name: "AadhaarLastFour",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedAadhaarNumber",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShareContactDetails",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "suppliers",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "suppliers",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShrinkageReason",
                table: "stock_adjustments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AbcCategory",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductType",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SafetyStockQty",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "QualityCheckNotes",
                table: "goods_receipt_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QualityCheckStatus",
                table: "goods_receipt_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AuditLogArchives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    OldValues = table.Column<string>(type: "text", nullable: true),
                    NewValues = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    OriginalCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogArchives", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("b0d33b91-4567-4eef-b123-888888888801"),
                columns: new[] { "AadhaarLastFour", "EncryptedAadhaarNumber", "ShareContactDetails" },
                values: new object[] { null, null, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogArchives");

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

            migrationBuilder.DropColumn(
                name: "AadhaarLastFour",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EncryptedAadhaarNumber",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ShareContactDetails",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ShrinkageReason",
                table: "stock_adjustments");

            migrationBuilder.DropColumn(
                name: "AbcCategory",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "products");

            migrationBuilder.DropColumn(
                name: "SafetyStockQty",
                table: "products");

            migrationBuilder.DropColumn(
                name: "QualityCheckNotes",
                table: "goods_receipt_items");

            migrationBuilder.DropColumn(
                name: "QualityCheckStatus",
                table: "goods_receipt_items");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "suppliers",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "suppliers",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldMaxLength: 250);
        }
    }
}
