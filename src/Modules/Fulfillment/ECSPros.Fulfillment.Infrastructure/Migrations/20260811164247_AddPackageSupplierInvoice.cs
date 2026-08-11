using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Fulfillment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageSupplierInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupplierInvoiceNumber",
                schema: "fulfillment",
                table: "ful_packages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierInvoiceUrl",
                schema: "fulfillment",
                table: "ful_packages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupplierInvoiceNumber",
                schema: "fulfillment",
                table: "ful_packages");

            migrationBuilder.DropColumn(
                name: "SupplierInvoiceUrl",
                schema: "fulfillment",
                table: "ful_packages");
        }
    }
}
