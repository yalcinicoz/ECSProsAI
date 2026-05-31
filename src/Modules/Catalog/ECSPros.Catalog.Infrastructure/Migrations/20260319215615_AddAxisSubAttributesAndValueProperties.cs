using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAxisSubAttributesAndValueProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_attribute_value_properties",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_catalog_attribute_value_properties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_attribute_value_properties_catalog_attribute_types_~",
                        column: x => x.SubAttributeTypeId,
                        principalSchema: "catalog",
                        principalTable: "catalog_attribute_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_attribute_value_properties_catalog_attribute_values~",
                        column: x => x.AttributeValueId,
                        principalSchema: "catalog",
                        principalTable: "catalog_attribute_values",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "catalog_product_group_axis_sub_attributes",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    AxisAttributeTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubAttributeTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_catalog_product_group_axis_sub_attributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_product_group_axis_sub_attributes_catalog_attribute~",
                        column: x => x.AxisAttributeTypeId,
                        principalSchema: "catalog",
                        principalTable: "catalog_attribute_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_product_group_axis_sub_attributes_catalog_attribut~1",
                        column: x => x.SubAttributeTypeId,
                        principalSchema: "catalog",
                        principalTable: "catalog_attribute_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_product_group_axis_sub_attributes_catalog_product_g~",
                        column: x => x.ProductGroupId,
                        principalSchema: "catalog",
                        principalTable: "catalog_product_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_attribute_value_properties_AttributeValueId_SubAttr~",
                schema: "catalog",
                table: "catalog_attribute_value_properties",
                columns: new[] { "AttributeValueId", "SubAttributeTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_attribute_value_properties_SubAttributeTypeId",
                schema: "catalog",
                table: "catalog_attribute_value_properties",
                column: "SubAttributeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_group_axis_sub_attributes_AxisAttributeType~",
                schema: "catalog",
                table: "catalog_product_group_axis_sub_attributes",
                column: "AxisAttributeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_group_axis_sub_attributes_ProductGroupId_Ax~",
                schema: "catalog",
                table: "catalog_product_group_axis_sub_attributes",
                columns: new[] { "ProductGroupId", "AxisAttributeTypeId", "SubAttributeTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_group_axis_sub_attributes_SubAttributeTypeId",
                schema: "catalog",
                table: "catalog_product_group_axis_sub_attributes",
                column: "SubAttributeTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_attribute_value_properties",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_product_group_axis_sub_attributes",
                schema: "catalog");
        }
    }
}
