using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyOrderId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LegacyOrderId",
                schema: "order",
                table: "ord_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ord_orders_LegacyOrderId",
                schema: "order",
                table: "ord_orders",
                column: "LegacyOrderId",
                unique: true,
                filter: "\"LegacyOrderId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ord_orders_LegacyOrderId",
                schema: "order",
                table: "ord_orders");

            migrationBuilder.DropColumn(
                name: "LegacyOrderId",
                schema: "order",
                table: "ord_orders");
        }
    }
}
