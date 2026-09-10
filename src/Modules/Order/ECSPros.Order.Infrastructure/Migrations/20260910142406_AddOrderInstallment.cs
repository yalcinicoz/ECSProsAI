using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderInstallment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InstallmentCount",
                schema: "order",
                table: "ord_orders",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "InstallmentFee",
                schema: "order",
                table: "ord_orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_ord_returns_OrderId",
                schema: "order",
                table: "ord_returns",
                column: "OrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_ord_returns_ord_orders_OrderId",
                schema: "order",
                table: "ord_returns",
                column: "OrderId",
                principalSchema: "order",
                principalTable: "ord_orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ord_returns_ord_orders_OrderId",
                schema: "order",
                table: "ord_returns");

            migrationBuilder.DropIndex(
                name: "IX_ord_returns_OrderId",
                schema: "order",
                table: "ord_returns");

            migrationBuilder.DropColumn(
                name: "InstallmentCount",
                schema: "order",
                table: "ord_orders");

            migrationBuilder.DropColumn(
                name: "InstallmentFee",
                schema: "order",
                table: "ord_orders");
        }
    }
}
