using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiValueProductAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_product_variant_attributes_VariantId_AttributeTypeId",
                schema: "catalog",
                table: "product_variant_attributes");

            migrationBuilder.DropIndex(
                name: "IX_product_attributes_ProductId_AttributeTypeId",
                schema: "catalog",
                table: "product_attributes");

            migrationBuilder.CreateIndex(
                name: "IX_product_variant_attributes_VariantId_AttributeTypeId_Attrib~",
                schema: "catalog",
                table: "product_variant_attributes",
                columns: new[] { "VariantId", "AttributeTypeId", "AttributeValueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_attributes_ProductId_AttributeTypeId_AttributeValue~",
                schema: "catalog",
                table: "product_attributes",
                columns: new[] { "ProductId", "AttributeTypeId", "AttributeValueId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_product_variant_attributes_VariantId_AttributeTypeId_Attrib~",
                schema: "catalog",
                table: "product_variant_attributes");

            migrationBuilder.DropIndex(
                name: "IX_product_attributes_ProductId_AttributeTypeId_AttributeValue~",
                schema: "catalog",
                table: "product_attributes");

            migrationBuilder.CreateIndex(
                name: "IX_product_variant_attributes_VariantId_AttributeTypeId",
                schema: "catalog",
                table: "product_variant_attributes",
                columns: new[] { "VariantId", "AttributeTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_attributes_ProductId_AttributeTypeId",
                schema: "catalog",
                table: "product_attributes",
                columns: new[] { "ProductId", "AttributeTypeId" },
                unique: true);
        }
    }
}
