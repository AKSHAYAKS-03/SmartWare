using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAdminSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("b0d33b91-4567-4eef-b123-888888888801"),
                columns: new[] { "PasswordHash", "Status" },
                values: new object[] { "$2a$11$PoX6gPtxzAqiQq6Eht0mqOR5Snv/5XxOJA7Bl3P2bH89dAWIw5BD.", 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("b0d33b91-4567-4eef-b123-888888888801"),
                columns: new[] { "PasswordHash", "Status" },
                values: new object[] { "$2a$11$W2.D1u5q0vF9lWlJpM6z1eZ6gCshK/2mZ/9fL1Z3O9fGqJ2Q5k4z2", 0 });
        }
    }
}
