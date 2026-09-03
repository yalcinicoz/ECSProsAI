using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyCommerceIdentities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LegacyReturnId",
                schema: "order",
                table: "ord_returns",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegacyReturnItemId",
                schema: "order",
                table: "ord_return_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegacyOrderPaymentId",
                schema: "order",
                table: "ord_order_payments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegacyOrderLineId",
                schema: "order",
                table: "ord_order_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegacyInvoiceId",
                schema: "order",
                table: "ord_invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ord_returns_LegacyReturnId",
                schema: "order",
                table: "ord_returns",
                column: "LegacyReturnId",
                unique: true,
                filter: "\"LegacyReturnId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ord_return_items_LegacyReturnItemId",
                schema: "order",
                table: "ord_return_items",
                column: "LegacyReturnItemId",
                unique: true,
                filter: "\"LegacyReturnItemId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ord_order_payments_LegacyOrderPaymentId",
                schema: "order",
                table: "ord_order_payments",
                column: "LegacyOrderPaymentId",
                unique: true,
                filter: "\"LegacyOrderPaymentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ord_order_items_LegacyOrderLineId",
                schema: "order",
                table: "ord_order_items",
                column: "LegacyOrderLineId",
                unique: true,
                filter: "\"LegacyOrderLineId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ord_invoices_LegacyInvoiceId",
                schema: "order",
                table: "ord_invoices",
                column: "LegacyInvoiceId",
                unique: true,
                filter: "\"LegacyInvoiceId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ord_returns_LegacyReturnId",
                schema: "order",
                table: "ord_returns");

            migrationBuilder.DropIndex(
                name: "IX_ord_return_items_LegacyReturnItemId",
                schema: "order",
                table: "ord_return_items");

            migrationBuilder.DropIndex(
                name: "IX_ord_order_payments_LegacyOrderPaymentId",
                schema: "order",
                table: "ord_order_payments");

            migrationBuilder.DropIndex(
                name: "IX_ord_order_items_LegacyOrderLineId",
                schema: "order",
                table: "ord_order_items");

            migrationBuilder.DropIndex(
                name: "IX_ord_invoices_LegacyInvoiceId",
                schema: "order",
                table: "ord_invoices");

            migrationBuilder.DropColumn(
                name: "LegacyReturnId",
                schema: "order",
                table: "ord_returns");

            migrationBuilder.DropColumn(
                name: "LegacyReturnItemId",
                schema: "order",
                table: "ord_return_items");

            migrationBuilder.DropColumn(
                name: "LegacyOrderPaymentId",
                schema: "order",
                table: "ord_order_payments");

            migrationBuilder.DropColumn(
                name: "LegacyOrderLineId",
                schema: "order",
                table: "ord_order_items");

            migrationBuilder.DropColumn(
                name: "LegacyInvoiceId",
                schema: "order",
                table: "ord_invoices");
        }
    }
}
