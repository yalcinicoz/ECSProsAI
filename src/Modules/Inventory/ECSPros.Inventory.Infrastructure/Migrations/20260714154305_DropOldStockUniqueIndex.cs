using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropOldStockUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inv_stocks_VariantId_WarehouseId_LocationId_StockType",
                schema: "inventory",
                table: "inv_stocks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_inv_stocks_VariantId_WarehouseId_LocationId_StockType",
                schema: "inventory",
                table: "inv_stocks",
                columns: new[] { "VariantId", "WarehouseId", "LocationId", "StockType" },
                unique: true);
        }
    }
}
