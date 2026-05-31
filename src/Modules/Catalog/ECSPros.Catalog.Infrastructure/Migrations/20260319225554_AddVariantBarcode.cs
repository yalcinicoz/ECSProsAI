using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantBarcode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                schema: "catalog",
                table: "catalog_product_variants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_variants_Barcode",
                schema: "catalog",
                table: "catalog_product_variants",
                column: "Barcode",
                unique: true,
                filter: "\"Barcode\" IS NOT NULL AND \"Barcode\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_catalog_product_variants_Barcode",
                schema: "catalog",
                table: "catalog_product_variants");

            migrationBuilder.DropColumn(
                name: "Barcode",
                schema: "catalog",
                table: "catalog_product_variants");
        }
    }
}
