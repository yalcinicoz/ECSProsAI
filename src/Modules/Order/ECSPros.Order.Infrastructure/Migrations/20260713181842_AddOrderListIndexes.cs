using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ord_orders_CreatedAt",
                schema: "order",
                table: "ord_orders",
                column: "CreatedAt",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ord_orders_Status_CreatedAt",
                schema: "order",
                table: "ord_orders",
                columns: new[] { "Status", "CreatedAt" },
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ord_orders_CreatedAt",
                schema: "order",
                table: "ord_orders");

            migrationBuilder.DropIndex(
                name: "IX_ord_orders_Status_CreatedAt",
                schema: "order",
                table: "ord_orders");
        }
    }
}
