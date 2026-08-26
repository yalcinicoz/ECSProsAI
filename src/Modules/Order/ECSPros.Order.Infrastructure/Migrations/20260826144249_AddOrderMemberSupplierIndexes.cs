using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderMemberSupplierIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ord_orders_MemberId",
                schema: "order",
                table: "ord_orders",
                column: "MemberId",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ord_order_items_SupplierId",
                schema: "order",
                table: "ord_order_items",
                column: "SupplierId",
                filter: "\"SupplierId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ord_orders_MemberId",
                schema: "order",
                table: "ord_orders");

            migrationBuilder.DropIndex(
                name: "IX_ord_order_items_SupplierId",
                schema: "order",
                table: "ord_order_items");
        }
    }
}
