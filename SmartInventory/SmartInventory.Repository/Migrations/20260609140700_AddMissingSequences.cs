using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "seq_products");

            migrationBuilder.CreateSequence<int>(
                name: "seq_warehouses");

            migrationBuilder.CreateSequence<int>(
                name: "seq_zones");

            migrationBuilder.CreateSequence<int>(
                name: "seq_bins");

            migrationBuilder.CreateSequence<int>(
                name: "seq_invoices");

           

            migrationBuilder.CreateSequence<int>(
                name: "seq_tracking_numbers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "seq_products");

            migrationBuilder.DropSequence(
                name: "seq_warehouses");

            migrationBuilder.DropSequence(
                name: "seq_zones");

            migrationBuilder.DropSequence(
                name: "seq_bins");

            migrationBuilder.DropSequence(
                name: "seq_invoices");

            

            migrationBuilder.DropSequence(
                name: "seq_tracking_numbers");
        }
    }
}
