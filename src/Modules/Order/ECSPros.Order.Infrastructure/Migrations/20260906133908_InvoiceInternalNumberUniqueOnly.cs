using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceInternalNumberUniqueOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ord_invoices_InvoiceSerial_InvoiceYear_InvoiceSequence",
                schema: "order",
                table: "ord_invoices");

            migrationBuilder.CreateIndex(
                name: "IX_ord_invoices_InvoiceSerial_InvoiceYear_InvoiceSequence",
                schema: "order",
                table: "ord_invoices",
                columns: new[] { "InvoiceSerial", "InvoiceYear", "InvoiceSequence" },
                unique: true,
                filter: "\"NumberSource\" = 'internal'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ord_invoices_InvoiceSerial_InvoiceYear_InvoiceSequence",
                schema: "order",
                table: "ord_invoices");

            migrationBuilder.CreateIndex(
                name: "IX_ord_invoices_InvoiceSerial_InvoiceYear_InvoiceSequence",
                schema: "order",
                table: "ord_invoices",
                columns: new[] { "InvoiceSerial", "InvoiceYear", "InvoiceSequence" },
                unique: true);
        }
    }
}
