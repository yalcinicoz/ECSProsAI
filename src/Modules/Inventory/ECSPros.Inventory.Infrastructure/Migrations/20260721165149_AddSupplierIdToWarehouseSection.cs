using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierIdToWarehouseSection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                schema: "inventory",
                table: "inv_warehouse_sections",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_inv_warehouse_sections_SupplierId",
                schema: "inventory",
                table: "inv_warehouse_sections",
                column: "SupplierId",
                filter: "\"SupplierId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inv_warehouse_sections_SupplierId",
                schema: "inventory",
                table: "inv_warehouse_sections");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                schema: "inventory",
                table: "inv_warehouse_sections");
        }
    }
}
