using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductAxisSubAttributeValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_catalog_products_Slug",
                schema: "catalog",
                table: "catalog_products");

            migrationBuilder.CreateTable(
                name: "catalog_product_axis_sub_attribute_values",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeValueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubAttributeTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_product_axis_sub_attribute_values", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_product_axis_sub_attribute_values_catalog_attribute~",
                        column: x => x.AttributeValueId,
                        principalSchema: "catalog",
                        principalTable: "catalog_attribute_values",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_catalog_product_axis_sub_attribute_values_catalog_attribut~1",
                        column: x => x.SubAttributeTypeId,
                        principalSchema: "catalog",
                        principalTable: "catalog_attribute_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_product_axis_sub_attribute_values_catalog_products_~",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "catalog_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_products_Slug",
                schema: "catalog",
                table: "catalog_products",
                column: "Slug",
                unique: true,
                filter: "\"Slug\" IS NOT NULL AND \"Slug\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_axis_sub_attribute_values_AttributeValueId",
                schema: "catalog",
                table: "catalog_product_axis_sub_attribute_values",
                column: "AttributeValueId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_axis_sub_attribute_values_ProductId_Attribu~",
                schema: "catalog",
                table: "catalog_product_axis_sub_attribute_values",
                columns: new[] { "ProductId", "AttributeValueId", "SubAttributeTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_axis_sub_attribute_values_SubAttributeTypeId",
                schema: "catalog",
                table: "catalog_product_axis_sub_attribute_values",
                column: "SubAttributeTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_product_axis_sub_attribute_values",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_catalog_products_Slug",
                schema: "catalog",
                table: "catalog_products");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_products_Slug",
                schema: "catalog",
                table: "catalog_products",
                column: "Slug",
                unique: true,
                filter: "slug IS NOT NULL AND slug <> ''");
        }
    }
}
