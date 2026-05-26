using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddEnterpriseVettingAndCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "warehouses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                table: "warehouses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNumber",
                table: "warehouses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "warehouses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TaxIdentifier",
                table: "warehouses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedById",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeId",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("b0d33b91-4567-4eef-b123-888888888801"),
                columns: new[] { "ApprovedAt", "ApprovedById", "EmployeeId" },
                values: new object[] { null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_ApprovedById",
                table: "warehouses",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_RegistrationNumber",
                table: "warehouses",
                column: "RegistrationNumber",
                unique: true,
                filter: "\"RegistrationNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_Status",
                table: "warehouses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_TaxIdentifier",
                table: "warehouses",
                column: "TaxIdentifier",
                unique: true,
                filter: "\"TaxIdentifier\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_users_ApprovedById",
                table: "users",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_users_EmployeeId",
                table: "users",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Status",
                table: "users",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_users_users_ApprovedById",
                table: "users",
                column: "ApprovedById",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_warehouses_users_ApprovedById",
                table: "warehouses",
                column: "ApprovedById",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_users_ApprovedById",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_warehouses_users_ApprovedById",
                table: "warehouses");

            migrationBuilder.DropIndex(
                name: "IX_warehouses_ApprovedById",
                table: "warehouses");

            migrationBuilder.DropIndex(
                name: "IX_warehouses_RegistrationNumber",
                table: "warehouses");

            migrationBuilder.DropIndex(
                name: "IX_warehouses_Status",
                table: "warehouses");

            migrationBuilder.DropIndex(
                name: "IX_warehouses_TaxIdentifier",
                table: "warehouses");

            migrationBuilder.DropIndex(
                name: "IX_users_ApprovedById",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_EmployeeId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_Status",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "RegistrationNumber",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "TaxIdentifier",
                table: "warehouses");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "users");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "users");
        }
    }
}
