using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockBinAndReservationLegacyRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BinId",
                schema: "inventory",
                table: "inv_stocks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SectionId",
                schema: "inventory",
                table: "inv_stocks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LegacyReferenceId",
                schema: "inventory",
                table: "inv_stock_reservations",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_inv_stocks_VariantId_BinId",
                schema: "inventory",
                table: "inv_stocks",
                columns: new[] { "VariantId", "BinId" },
                unique: true,
                filter: "\"BinId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inv_stocks_VariantId_BinId",
                schema: "inventory",
                table: "inv_stocks");

            migrationBuilder.DropColumn(
                name: "BinId",
                schema: "inventory",
                table: "inv_stocks");

            migrationBuilder.DropColumn(
                name: "SectionId",
                schema: "inventory",
                table: "inv_stocks");

            migrationBuilder.DropColumn(
                name: "LegacyReferenceId",
                schema: "inventory",
                table: "inv_stock_reservations");
        }
    }
}
