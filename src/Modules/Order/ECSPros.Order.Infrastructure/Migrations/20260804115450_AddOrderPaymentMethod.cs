using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPaymentMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                schema: "order",
                table: "ord_orders",
                type: "text",
                nullable: true);

            // Geri doldurma: yöntem şimdiye dek CustomerNotes jsonb'sinde tutuluyordu
            // (checkout 'paymentMethod' anahtarı) — mevcut kayıtlar kolona taşınır.
            migrationBuilder.Sql("""
                UPDATE "order".ord_orders
                SET "PaymentMethod" = "CustomerNotes"->>'paymentMethod'
                WHERE "CustomerNotes" ? 'paymentMethod';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                schema: "order",
                table: "ord_orders");
        }
    }
}
