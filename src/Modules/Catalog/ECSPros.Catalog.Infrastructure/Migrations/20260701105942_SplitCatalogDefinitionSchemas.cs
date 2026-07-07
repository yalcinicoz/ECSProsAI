using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitCatalogDefinitionSchemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_catalog_attribute_values_catalog_attribute_types_AttributeT~",
                schema: "catalog",
                table: "catalog_attribute_values");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_firm_platform_products_catalog_products_ProductId",
                schema: "catalog",
                table: "catalog_firm_platform_products");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_firm_platform_variants_catalog_product_variants_Var~",
                schema: "catalog",
                table: "catalog_firm_platform_variants");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_image_sets_catalog_image_sets_FallbackSetId",
                schema: "catalog",
                table: "catalog_image_sets");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_attributes_catalog_attribute_types_Attribut~",
                schema: "catalog",
                table: "catalog_product_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_attributes_catalog_attribute_values_Attribu~",
                schema: "catalog",
                table: "catalog_product_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_attributes_catalog_products_ProductId",
                schema: "catalog",
                table: "catalog_product_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_axis_sub_attribute_values_catalog_attribute~",
                schema: "catalog",
                table: "catalog_product_axis_sub_attribute_values");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_axis_sub_attribute_values_catalog_attribut~1",
                schema: "catalog",
                table: "catalog_product_axis_sub_attribute_values");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_axis_sub_attribute_values_catalog_products_~",
                schema: "catalog",
                table: "catalog_product_axis_sub_attribute_values");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_group_attributes_catalog_attribute_types_At~",
                schema: "catalog",
                table: "catalog_product_group_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_group_attributes_catalog_product_groups_Pro~",
                schema: "catalog",
                table: "catalog_product_group_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_group_axis_sub_attributes_catalog_attribute~",
                schema: "catalog",
                table: "catalog_product_group_axis_sub_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_group_axis_sub_attributes_catalog_attribut~1",
                schema: "catalog",
                table: "catalog_product_group_axis_sub_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_group_axis_sub_attributes_catalog_product_g~",
                schema: "catalog",
                table: "catalog_product_group_axis_sub_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_image_set_mappings_catalog_image_sets_ForSe~",
                schema: "catalog",
                table: "catalog_product_image_set_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_image_set_mappings_catalog_image_sets_UseSe~",
                schema: "catalog",
                table: "catalog_product_image_set_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_image_set_mappings_catalog_products_Product~",
                schema: "catalog",
                table: "catalog_product_image_set_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_images_catalog_image_sets_ImageSetId",
                schema: "catalog",
                table: "catalog_product_images");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_images_catalog_product_variants_VariantId",
                schema: "catalog",
                table: "catalog_product_images");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_images_catalog_products_ProductId",
                schema: "catalog",
                table: "catalog_product_images");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_price_history_catalog_products_ProductId",
                schema: "catalog",
                table: "catalog_product_price_history");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_units_catalog_product_variants_VariantId",
                schema: "catalog",
                table: "catalog_product_units");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_variant_attributes_catalog_attribute_types_~",
                schema: "catalog",
                table: "catalog_product_variant_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_variant_attributes_catalog_attribute_values~",
                schema: "catalog",
                table: "catalog_product_variant_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_variant_attributes_catalog_product_variants~",
                schema: "catalog",
                table: "catalog_product_variant_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_variant_images_catalog_product_variants_Var~",
                schema: "catalog",
                table: "catalog_product_variant_images");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_variants_catalog_products_ProductId",
                schema: "catalog",
                table: "catalog_product_variants");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_videos_catalog_image_sets_ImageSetId",
                schema: "catalog",
                table: "catalog_product_videos");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_videos_catalog_products_ProductId",
                schema: "catalog",
                table: "catalog_product_videos");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_products_catalog_product_groups_ProductGroupId",
                schema: "catalog",
                table: "catalog_products");

            migrationBuilder.DropForeignKey(
                name: "FK_catalog_variant_price_history_catalog_product_variants_Vari~",
                schema: "catalog",
                table: "catalog_variant_price_history");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_variant_price_history",
                schema: "catalog",
                table: "catalog_variant_price_history");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_settings",
                schema: "catalog",
                table: "catalog_settings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_products",
                schema: "catalog",
                table: "catalog_products");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_product_videos",
                schema: "catalog",
                table: "catalog_product_videos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_product_variants",
                schema: "catalog",
                table: "catalog_product_variants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_product_variant_images",
                schema: "catalog",
                table: "catalog_product_variant_images");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_product_variant_attributes",
                schema: "catalog",
                table: "catalog_product_variant_attributes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_product_units",
                schema: "catalog",
                table: "catalog_product_units");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_product_price_history",
                schema: "catalog",
                table: "catalog_product_price_history");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_product_images",
                schema: "catalog",
                table: "catalog_product_images");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_product_image_set_mappings",
                schema: "catalog",
                table: "catalog_product_image_set_mappings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_product_groups",
                schema: "catalog",
                table: "catalog_product_groups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_product_group_axis_sub_attributes",
                schema: "catalog",
                table: "catalog_product_group_axis_sub_attributes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_product_group_attributes",
                schema: "catalog",
                table: "catalog_product_group_attributes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_product_axis_sub_attribute_values",
                schema: "catalog",
                table: "catalog_product_axis_sub_attribute_values");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_product_attributes",
                schema: "catalog",
                table: "catalog_product_attributes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_image_sets",
                schema: "catalog",
                table: "catalog_image_sets");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_firm_platform_variants",
                schema: "catalog",
                table: "catalog_firm_platform_variants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_firm_platform_products",
                schema: "catalog",
                table: "catalog_firm_platform_products");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_attribute_values",
                schema: "catalog",
                table: "catalog_attribute_values");

            migrationBuilder.DropPrimaryKey(
                name: "PK_catalog_attribute_types",
                schema: "catalog",
                table: "catalog_attribute_types");

            migrationBuilder.EnsureSchema(
                name: "definition");

            migrationBuilder.RenameTable(
                name: "catalog_variant_price_history",
                schema: "catalog",
                newName: "variant_price_history",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "catalog_settings",
                schema: "catalog",
                newName: "settings",
                newSchema: "definition");

            migrationBuilder.RenameTable(
                name: "catalog_products",
                schema: "catalog",
                newName: "products",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "catalog_product_videos",
                schema: "catalog",
                newName: "product_videos",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "catalog_product_variants",
                schema: "catalog",
                newName: "product_variants",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "catalog_product_variant_images",
                schema: "catalog",
                newName: "product_variant_images",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "catalog_product_variant_attributes",
                schema: "catalog",
                newName: "product_variant_attributes",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "catalog_product_units",
                schema: "catalog",
                newName: "product_units",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "catalog_product_price_history",
                schema: "catalog",
                newName: "product_price_history",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "catalog_product_images",
                schema: "catalog",
                newName: "product_images",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "catalog_product_image_set_mappings",
                schema: "catalog",
                newName: "product_image_set_mappings",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "catalog_product_groups",
                schema: "catalog",
                newName: "product_groups",
                newSchema: "definition");

            migrationBuilder.RenameTable(
                name: "catalog_product_group_axis_sub_attributes",
                schema: "catalog",
                newName: "product_group_axis_sub_attributes",
                newSchema: "definition");

            migrationBuilder.RenameTable(
                name: "catalog_product_group_attributes",
                schema: "catalog",
                newName: "product_group_attributes",
                newSchema: "definition");

            migrationBuilder.RenameTable(
                name: "catalog_product_axis_sub_attribute_values",
                schema: "catalog",
                newName: "product_axis_sub_attribute_values",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "catalog_product_attributes",
                schema: "catalog",
                newName: "product_attributes",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "catalog_image_sets",
                schema: "catalog",
                newName: "image_sets",
                newSchema: "definition");

            migrationBuilder.RenameTable(
                name: "catalog_firm_platform_variants",
                schema: "catalog",
                newName: "firm_platform_variants",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "catalog_firm_platform_products",
                schema: "catalog",
                newName: "firm_platform_products",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "catalog_attribute_values",
                schema: "catalog",
                newName: "attribute_values",
                newSchema: "definition");

            migrationBuilder.RenameTable(
                name: "catalog_attribute_types",
                schema: "catalog",
                newName: "attribute_types",
                newSchema: "definition");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_variant_price_history_VariantId",
                schema: "catalog",
                table: "variant_price_history",
                newName: "IX_variant_price_history_VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_products_Slug",
                schema: "catalog",
                table: "products",
                newName: "IX_products_Slug");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_products_ProductGroupId",
                schema: "catalog",
                table: "products",
                newName: "IX_products_ProductGroupId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_products_Code",
                schema: "catalog",
                table: "products",
                newName: "IX_products_Code");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_videos_ProductId_ImageSetId_Status",
                schema: "catalog",
                table: "product_videos",
                newName: "IX_product_videos_ProductId_ImageSetId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_videos_ImageSetId",
                schema: "catalog",
                table: "product_videos",
                newName: "IX_product_videos_ImageSetId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_videos_BatchId",
                schema: "catalog",
                table: "product_videos",
                newName: "IX_product_videos_BatchId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_variants_Sku",
                schema: "catalog",
                table: "product_variants",
                newName: "IX_product_variants_Sku");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_variants_ProductId",
                schema: "catalog",
                table: "product_variants",
                newName: "IX_product_variants_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_variants_Barcode",
                schema: "catalog",
                table: "product_variants",
                newName: "IX_product_variants_Barcode");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_variant_images_VariantId",
                schema: "catalog",
                table: "product_variant_images",
                newName: "IX_product_variant_images_VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_variant_attributes_VariantId_AttributeTypeId",
                schema: "catalog",
                table: "product_variant_attributes",
                newName: "IX_product_variant_attributes_VariantId_AttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_variant_attributes_AttributeValueId",
                schema: "catalog",
                table: "product_variant_attributes",
                newName: "IX_product_variant_attributes_AttributeValueId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_variant_attributes_AttributeTypeId",
                schema: "catalog",
                table: "product_variant_attributes",
                newName: "IX_product_variant_attributes_AttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_units_VariantId_UnitType",
                schema: "catalog",
                table: "product_units",
                newName: "IX_product_units_VariantId_UnitType");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_price_history_ProductId",
                schema: "catalog",
                table: "product_price_history",
                newName: "IX_product_price_history_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_price_history_ChangedAt",
                schema: "catalog",
                table: "product_price_history",
                newName: "IX_product_price_history_ChangedAt");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_images_VariantId",
                schema: "catalog",
                table: "product_images",
                newName: "IX_product_images_VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_images_ProductId_ImageSetId_Status",
                schema: "catalog",
                table: "product_images",
                newName: "IX_product_images_ProductId_ImageSetId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_images_ImageSetId",
                schema: "catalog",
                table: "product_images",
                newName: "IX_product_images_ImageSetId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_images_BatchId",
                schema: "catalog",
                table: "product_images",
                newName: "IX_product_images_BatchId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_image_set_mappings_UseSetId",
                schema: "catalog",
                table: "product_image_set_mappings",
                newName: "IX_product_image_set_mappings_UseSetId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_image_set_mappings_ProductId_ForSetId",
                schema: "catalog",
                table: "product_image_set_mappings",
                newName: "IX_product_image_set_mappings_ProductId_ForSetId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_image_set_mappings_ForSetId",
                schema: "catalog",
                table: "product_image_set_mappings",
                newName: "IX_product_image_set_mappings_ForSetId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_groups_Code",
                schema: "definition",
                table: "product_groups",
                newName: "IX_product_groups_Code");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_group_axis_sub_attributes_SubAttributeTypeId",
                schema: "definition",
                table: "product_group_axis_sub_attributes",
                newName: "IX_product_group_axis_sub_attributes_SubAttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_group_axis_sub_attributes_ProductGroupId_Ax~",
                schema: "definition",
                table: "product_group_axis_sub_attributes",
                newName: "IX_product_group_axis_sub_attributes_ProductGroupId_AxisAttrib~");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_group_axis_sub_attributes_AxisAttributeType~",
                schema: "definition",
                table: "product_group_axis_sub_attributes",
                newName: "IX_product_group_axis_sub_attributes_AxisAttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_group_attributes_ProductGroupId_AttributeTy~",
                schema: "definition",
                table: "product_group_attributes",
                newName: "IX_product_group_attributes_ProductGroupId_AttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_group_attributes_AttributeTypeId",
                schema: "definition",
                table: "product_group_attributes",
                newName: "IX_product_group_attributes_AttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_axis_sub_attribute_values_SubAttributeTypeId",
                schema: "catalog",
                table: "product_axis_sub_attribute_values",
                newName: "IX_product_axis_sub_attribute_values_SubAttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_axis_sub_attribute_values_ProductId_Attribu~",
                schema: "catalog",
                table: "product_axis_sub_attribute_values",
                newName: "IX_product_axis_sub_attribute_values_ProductId_AttributeValueI~");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_axis_sub_attribute_values_AttributeValueId",
                schema: "catalog",
                table: "product_axis_sub_attribute_values",
                newName: "IX_product_axis_sub_attribute_values_AttributeValueId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_attributes_ProductId_AttributeTypeId",
                schema: "catalog",
                table: "product_attributes",
                newName: "IX_product_attributes_ProductId_AttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_attributes_AttributeValueId",
                schema: "catalog",
                table: "product_attributes",
                newName: "IX_product_attributes_AttributeValueId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_product_attributes_AttributeTypeId",
                schema: "catalog",
                table: "product_attributes",
                newName: "IX_product_attributes_AttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_image_sets_FallbackSetId",
                schema: "definition",
                table: "image_sets",
                newName: "IX_image_sets_FallbackSetId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_image_sets_Code",
                schema: "definition",
                table: "image_sets",
                newName: "IX_image_sets_Code");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_firm_platform_variants_VariantId",
                schema: "catalog",
                table: "firm_platform_variants",
                newName: "IX_firm_platform_variants_VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_firm_platform_variants_FirmPlatformId_VariantId",
                schema: "catalog",
                table: "firm_platform_variants",
                newName: "IX_firm_platform_variants_FirmPlatformId_VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_firm_platform_products_ProductId",
                schema: "catalog",
                table: "firm_platform_products",
                newName: "IX_firm_platform_products_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_firm_platform_products_FirmPlatformId_ProductId",
                schema: "catalog",
                table: "firm_platform_products",
                newName: "IX_firm_platform_products_FirmPlatformId_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_attribute_values_AttributeTypeId",
                schema: "definition",
                table: "attribute_values",
                newName: "IX_attribute_values_AttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_catalog_attribute_types_Code",
                schema: "definition",
                table: "attribute_types",
                newName: "IX_attribute_types_Code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_variant_price_history",
                schema: "catalog",
                table: "variant_price_history",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_settings",
                schema: "definition",
                table: "settings",
                column: "Key");

            migrationBuilder.AddPrimaryKey(
                name: "PK_products",
                schema: "catalog",
                table: "products",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_product_videos",
                schema: "catalog",
                table: "product_videos",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_product_variants",
                schema: "catalog",
                table: "product_variants",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_product_variant_images",
                schema: "catalog",
                table: "product_variant_images",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_product_variant_attributes",
                schema: "catalog",
                table: "product_variant_attributes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_product_units",
                schema: "catalog",
                table: "product_units",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_product_price_history",
                schema: "catalog",
                table: "product_price_history",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_product_images",
                schema: "catalog",
                table: "product_images",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_product_image_set_mappings",
                schema: "catalog",
                table: "product_image_set_mappings",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_product_groups",
                schema: "definition",
                table: "product_groups",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_product_group_axis_sub_attributes",
                schema: "definition",
                table: "product_group_axis_sub_attributes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_product_group_attributes",
                schema: "definition",
                table: "product_group_attributes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_product_axis_sub_attribute_values",
                schema: "catalog",
                table: "product_axis_sub_attribute_values",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_product_attributes",
                schema: "catalog",
                table: "product_attributes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_image_sets",
                schema: "definition",
                table: "image_sets",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_firm_platform_variants",
                schema: "catalog",
                table: "firm_platform_variants",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_firm_platform_products",
                schema: "catalog",
                table: "firm_platform_products",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_attribute_values",
                schema: "definition",
                table: "attribute_values",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_attribute_types",
                schema: "definition",
                table: "attribute_types",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_attribute_values_attribute_types_AttributeTypeId",
                schema: "definition",
                table: "attribute_values",
                column: "AttributeTypeId",
                principalSchema: "definition",
                principalTable: "attribute_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_firm_platform_products_products_ProductId",
                schema: "catalog",
                table: "firm_platform_products",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_firm_platform_variants_product_variants_VariantId",
                schema: "catalog",
                table: "firm_platform_variants",
                column: "VariantId",
                principalSchema: "catalog",
                principalTable: "product_variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_image_sets_image_sets_FallbackSetId",
                schema: "definition",
                table: "image_sets",
                column: "FallbackSetId",
                principalSchema: "definition",
                principalTable: "image_sets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_product_attributes_attribute_types_AttributeTypeId",
                schema: "catalog",
                table: "product_attributes",
                column: "AttributeTypeId",
                principalSchema: "definition",
                principalTable: "attribute_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_attributes_attribute_values_AttributeValueId",
                schema: "catalog",
                table: "product_attributes",
                column: "AttributeValueId",
                principalSchema: "definition",
                principalTable: "attribute_values",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_product_attributes_products_ProductId",
                schema: "catalog",
                table: "product_attributes",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_axis_sub_attribute_values_attribute_types_SubAttrib~",
                schema: "catalog",
                table: "product_axis_sub_attribute_values",
                column: "SubAttributeTypeId",
                principalSchema: "definition",
                principalTable: "attribute_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_axis_sub_attribute_values_attribute_values_Attribut~",
                schema: "catalog",
                table: "product_axis_sub_attribute_values",
                column: "AttributeValueId",
                principalSchema: "definition",
                principalTable: "attribute_values",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_axis_sub_attribute_values_products_ProductId",
                schema: "catalog",
                table: "product_axis_sub_attribute_values",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_group_attributes_attribute_types_AttributeTypeId",
                schema: "definition",
                table: "product_group_attributes",
                column: "AttributeTypeId",
                principalSchema: "definition",
                principalTable: "attribute_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_group_attributes_product_groups_ProductGroupId",
                schema: "definition",
                table: "product_group_attributes",
                column: "ProductGroupId",
                principalSchema: "definition",
                principalTable: "product_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_group_axis_sub_attributes_attribute_types_AxisAttri~",
                schema: "definition",
                table: "product_group_axis_sub_attributes",
                column: "AxisAttributeTypeId",
                principalSchema: "definition",
                principalTable: "attribute_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_group_axis_sub_attributes_attribute_types_SubAttrib~",
                schema: "definition",
                table: "product_group_axis_sub_attributes",
                column: "SubAttributeTypeId",
                principalSchema: "definition",
                principalTable: "attribute_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_group_axis_sub_attributes_product_groups_ProductGro~",
                schema: "definition",
                table: "product_group_axis_sub_attributes",
                column: "ProductGroupId",
                principalSchema: "definition",
                principalTable: "product_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_image_set_mappings_image_sets_ForSetId",
                schema: "catalog",
                table: "product_image_set_mappings",
                column: "ForSetId",
                principalSchema: "definition",
                principalTable: "image_sets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_image_set_mappings_image_sets_UseSetId",
                schema: "catalog",
                table: "product_image_set_mappings",
                column: "UseSetId",
                principalSchema: "definition",
                principalTable: "image_sets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_image_set_mappings_products_ProductId",
                schema: "catalog",
                table: "product_image_set_mappings",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_images_image_sets_ImageSetId",
                schema: "catalog",
                table: "product_images",
                column: "ImageSetId",
                principalSchema: "definition",
                principalTable: "image_sets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_images_product_variants_VariantId",
                schema: "catalog",
                table: "product_images",
                column: "VariantId",
                principalSchema: "catalog",
                principalTable: "product_variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_product_images_products_ProductId",
                schema: "catalog",
                table: "product_images",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_price_history_products_ProductId",
                schema: "catalog",
                table: "product_price_history",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_units_product_variants_VariantId",
                schema: "catalog",
                table: "product_units",
                column: "VariantId",
                principalSchema: "catalog",
                principalTable: "product_variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_variant_attributes_attribute_types_AttributeTypeId",
                schema: "catalog",
                table: "product_variant_attributes",
                column: "AttributeTypeId",
                principalSchema: "definition",
                principalTable: "attribute_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_variant_attributes_attribute_values_AttributeValueId",
                schema: "catalog",
                table: "product_variant_attributes",
                column: "AttributeValueId",
                principalSchema: "definition",
                principalTable: "attribute_values",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_variant_attributes_product_variants_VariantId",
                schema: "catalog",
                table: "product_variant_attributes",
                column: "VariantId",
                principalSchema: "catalog",
                principalTable: "product_variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_variant_images_product_variants_VariantId",
                schema: "catalog",
                table: "product_variant_images",
                column: "VariantId",
                principalSchema: "catalog",
                principalTable: "product_variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_variants_products_ProductId",
                schema: "catalog",
                table: "product_variants",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_videos_image_sets_ImageSetId",
                schema: "catalog",
                table: "product_videos",
                column: "ImageSetId",
                principalSchema: "definition",
                principalTable: "image_sets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_videos_products_ProductId",
                schema: "catalog",
                table: "product_videos",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_products_product_groups_ProductGroupId",
                schema: "catalog",
                table: "products",
                column: "ProductGroupId",
                principalSchema: "definition",
                principalTable: "product_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_variant_price_history_product_variants_VariantId",
                schema: "catalog",
                table: "variant_price_history",
                column: "VariantId",
                principalSchema: "catalog",
                principalTable: "product_variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attribute_values_attribute_types_AttributeTypeId",
                schema: "definition",
                table: "attribute_values");

            migrationBuilder.DropForeignKey(
                name: "FK_firm_platform_products_products_ProductId",
                schema: "catalog",
                table: "firm_platform_products");

            migrationBuilder.DropForeignKey(
                name: "FK_firm_platform_variants_product_variants_VariantId",
                schema: "catalog",
                table: "firm_platform_variants");

            migrationBuilder.DropForeignKey(
                name: "FK_image_sets_image_sets_FallbackSetId",
                schema: "definition",
                table: "image_sets");

            migrationBuilder.DropForeignKey(
                name: "FK_product_attributes_attribute_types_AttributeTypeId",
                schema: "catalog",
                table: "product_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_product_attributes_attribute_values_AttributeValueId",
                schema: "catalog",
                table: "product_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_product_attributes_products_ProductId",
                schema: "catalog",
                table: "product_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_product_axis_sub_attribute_values_attribute_types_SubAttrib~",
                schema: "catalog",
                table: "product_axis_sub_attribute_values");

            migrationBuilder.DropForeignKey(
                name: "FK_product_axis_sub_attribute_values_attribute_values_Attribut~",
                schema: "catalog",
                table: "product_axis_sub_attribute_values");

            migrationBuilder.DropForeignKey(
                name: "FK_product_axis_sub_attribute_values_products_ProductId",
                schema: "catalog",
                table: "product_axis_sub_attribute_values");

            migrationBuilder.DropForeignKey(
                name: "FK_product_group_attributes_attribute_types_AttributeTypeId",
                schema: "definition",
                table: "product_group_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_product_group_attributes_product_groups_ProductGroupId",
                schema: "definition",
                table: "product_group_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_product_group_axis_sub_attributes_attribute_types_AxisAttri~",
                schema: "definition",
                table: "product_group_axis_sub_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_product_group_axis_sub_attributes_attribute_types_SubAttrib~",
                schema: "definition",
                table: "product_group_axis_sub_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_product_group_axis_sub_attributes_product_groups_ProductGro~",
                schema: "definition",
                table: "product_group_axis_sub_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_product_image_set_mappings_image_sets_ForSetId",
                schema: "catalog",
                table: "product_image_set_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_product_image_set_mappings_image_sets_UseSetId",
                schema: "catalog",
                table: "product_image_set_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_product_image_set_mappings_products_ProductId",
                schema: "catalog",
                table: "product_image_set_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_product_images_image_sets_ImageSetId",
                schema: "catalog",
                table: "product_images");

            migrationBuilder.DropForeignKey(
                name: "FK_product_images_product_variants_VariantId",
                schema: "catalog",
                table: "product_images");

            migrationBuilder.DropForeignKey(
                name: "FK_product_images_products_ProductId",
                schema: "catalog",
                table: "product_images");

            migrationBuilder.DropForeignKey(
                name: "FK_product_price_history_products_ProductId",
                schema: "catalog",
                table: "product_price_history");

            migrationBuilder.DropForeignKey(
                name: "FK_product_units_product_variants_VariantId",
                schema: "catalog",
                table: "product_units");

            migrationBuilder.DropForeignKey(
                name: "FK_product_variant_attributes_attribute_types_AttributeTypeId",
                schema: "catalog",
                table: "product_variant_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_product_variant_attributes_attribute_values_AttributeValueId",
                schema: "catalog",
                table: "product_variant_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_product_variant_attributes_product_variants_VariantId",
                schema: "catalog",
                table: "product_variant_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_product_variant_images_product_variants_VariantId",
                schema: "catalog",
                table: "product_variant_images");

            migrationBuilder.DropForeignKey(
                name: "FK_product_variants_products_ProductId",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropForeignKey(
                name: "FK_product_videos_image_sets_ImageSetId",
                schema: "catalog",
                table: "product_videos");

            migrationBuilder.DropForeignKey(
                name: "FK_product_videos_products_ProductId",
                schema: "catalog",
                table: "product_videos");

            migrationBuilder.DropForeignKey(
                name: "FK_products_product_groups_ProductGroupId",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_variant_price_history_product_variants_VariantId",
                schema: "catalog",
                table: "variant_price_history");

            migrationBuilder.DropPrimaryKey(
                name: "PK_variant_price_history",
                schema: "catalog",
                table: "variant_price_history");

            migrationBuilder.DropPrimaryKey(
                name: "PK_settings",
                schema: "definition",
                table: "settings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_products",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropPrimaryKey(
                name: "PK_product_videos",
                schema: "catalog",
                table: "product_videos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_product_variants",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_product_variant_images",
                schema: "catalog",
                table: "product_variant_images");

            migrationBuilder.DropPrimaryKey(
                name: "PK_product_variant_attributes",
                schema: "catalog",
                table: "product_variant_attributes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_product_units",
                schema: "catalog",
                table: "product_units");

            migrationBuilder.DropPrimaryKey(
                name: "PK_product_price_history",
                schema: "catalog",
                table: "product_price_history");

            migrationBuilder.DropPrimaryKey(
                name: "PK_product_images",
                schema: "catalog",
                table: "product_images");

            migrationBuilder.DropPrimaryKey(
                name: "PK_product_image_set_mappings",
                schema: "catalog",
                table: "product_image_set_mappings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_product_groups",
                schema: "definition",
                table: "product_groups");

            migrationBuilder.DropPrimaryKey(
                name: "PK_product_group_axis_sub_attributes",
                schema: "definition",
                table: "product_group_axis_sub_attributes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_product_group_attributes",
                schema: "definition",
                table: "product_group_attributes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_product_axis_sub_attribute_values",
                schema: "catalog",
                table: "product_axis_sub_attribute_values");

            migrationBuilder.DropPrimaryKey(
                name: "PK_product_attributes",
                schema: "catalog",
                table: "product_attributes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_image_sets",
                schema: "definition",
                table: "image_sets");

            migrationBuilder.DropPrimaryKey(
                name: "PK_firm_platform_variants",
                schema: "catalog",
                table: "firm_platform_variants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_firm_platform_products",
                schema: "catalog",
                table: "firm_platform_products");

            migrationBuilder.DropPrimaryKey(
                name: "PK_attribute_values",
                schema: "definition",
                table: "attribute_values");

            migrationBuilder.DropPrimaryKey(
                name: "PK_attribute_types",
                schema: "definition",
                table: "attribute_types");

            migrationBuilder.RenameTable(
                name: "variant_price_history",
                schema: "catalog",
                newName: "catalog_variant_price_history",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "settings",
                schema: "definition",
                newName: "catalog_settings",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "products",
                schema: "catalog",
                newName: "catalog_products",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "product_videos",
                schema: "catalog",
                newName: "catalog_product_videos",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "product_variants",
                schema: "catalog",
                newName: "catalog_product_variants",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "product_variant_images",
                schema: "catalog",
                newName: "catalog_product_variant_images",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "product_variant_attributes",
                schema: "catalog",
                newName: "catalog_product_variant_attributes",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "product_units",
                schema: "catalog",
                newName: "catalog_product_units",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "product_price_history",
                schema: "catalog",
                newName: "catalog_product_price_history",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "product_images",
                schema: "catalog",
                newName: "catalog_product_images",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "product_image_set_mappings",
                schema: "catalog",
                newName: "catalog_product_image_set_mappings",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "product_groups",
                schema: "definition",
                newName: "catalog_product_groups",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "product_group_axis_sub_attributes",
                schema: "definition",
                newName: "catalog_product_group_axis_sub_attributes",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "product_group_attributes",
                schema: "definition",
                newName: "catalog_product_group_attributes",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "product_axis_sub_attribute_values",
                schema: "catalog",
                newName: "catalog_product_axis_sub_attribute_values",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "product_attributes",
                schema: "catalog",
                newName: "catalog_product_attributes",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "image_sets",
                schema: "definition",
                newName: "catalog_image_sets",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "firm_platform_variants",
                schema: "catalog",
                newName: "catalog_firm_platform_variants",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "firm_platform_products",
                schema: "catalog",
                newName: "catalog_firm_platform_products",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "attribute_values",
                schema: "definition",
                newName: "catalog_attribute_values",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "attribute_types",
                schema: "definition",
                newName: "catalog_attribute_types",
                newSchema: "catalog");

            migrationBuilder.RenameIndex(
                name: "IX_variant_price_history_VariantId",
                schema: "catalog",
                table: "catalog_variant_price_history",
                newName: "IX_catalog_variant_price_history_VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_products_Slug",
                schema: "catalog",
                table: "catalog_products",
                newName: "IX_catalog_products_Slug");

            migrationBuilder.RenameIndex(
                name: "IX_products_ProductGroupId",
                schema: "catalog",
                table: "catalog_products",
                newName: "IX_catalog_products_ProductGroupId");

            migrationBuilder.RenameIndex(
                name: "IX_products_Code",
                schema: "catalog",
                table: "catalog_products",
                newName: "IX_catalog_products_Code");

            migrationBuilder.RenameIndex(
                name: "IX_product_videos_ProductId_ImageSetId_Status",
                schema: "catalog",
                table: "catalog_product_videos",
                newName: "IX_catalog_product_videos_ProductId_ImageSetId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_product_videos_ImageSetId",
                schema: "catalog",
                table: "catalog_product_videos",
                newName: "IX_catalog_product_videos_ImageSetId");

            migrationBuilder.RenameIndex(
                name: "IX_product_videos_BatchId",
                schema: "catalog",
                table: "catalog_product_videos",
                newName: "IX_catalog_product_videos_BatchId");

            migrationBuilder.RenameIndex(
                name: "IX_product_variants_Sku",
                schema: "catalog",
                table: "catalog_product_variants",
                newName: "IX_catalog_product_variants_Sku");

            migrationBuilder.RenameIndex(
                name: "IX_product_variants_ProductId",
                schema: "catalog",
                table: "catalog_product_variants",
                newName: "IX_catalog_product_variants_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_product_variants_Barcode",
                schema: "catalog",
                table: "catalog_product_variants",
                newName: "IX_catalog_product_variants_Barcode");

            migrationBuilder.RenameIndex(
                name: "IX_product_variant_images_VariantId",
                schema: "catalog",
                table: "catalog_product_variant_images",
                newName: "IX_catalog_product_variant_images_VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_product_variant_attributes_VariantId_AttributeTypeId",
                schema: "catalog",
                table: "catalog_product_variant_attributes",
                newName: "IX_catalog_product_variant_attributes_VariantId_AttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_product_variant_attributes_AttributeValueId",
                schema: "catalog",
                table: "catalog_product_variant_attributes",
                newName: "IX_catalog_product_variant_attributes_AttributeValueId");

            migrationBuilder.RenameIndex(
                name: "IX_product_variant_attributes_AttributeTypeId",
                schema: "catalog",
                table: "catalog_product_variant_attributes",
                newName: "IX_catalog_product_variant_attributes_AttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_product_units_VariantId_UnitType",
                schema: "catalog",
                table: "catalog_product_units",
                newName: "IX_catalog_product_units_VariantId_UnitType");

            migrationBuilder.RenameIndex(
                name: "IX_product_price_history_ProductId",
                schema: "catalog",
                table: "catalog_product_price_history",
                newName: "IX_catalog_product_price_history_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_product_price_history_ChangedAt",
                schema: "catalog",
                table: "catalog_product_price_history",
                newName: "IX_catalog_product_price_history_ChangedAt");

            migrationBuilder.RenameIndex(
                name: "IX_product_images_VariantId",
                schema: "catalog",
                table: "catalog_product_images",
                newName: "IX_catalog_product_images_VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_product_images_ProductId_ImageSetId_Status",
                schema: "catalog",
                table: "catalog_product_images",
                newName: "IX_catalog_product_images_ProductId_ImageSetId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_product_images_ImageSetId",
                schema: "catalog",
                table: "catalog_product_images",
                newName: "IX_catalog_product_images_ImageSetId");

            migrationBuilder.RenameIndex(
                name: "IX_product_images_BatchId",
                schema: "catalog",
                table: "catalog_product_images",
                newName: "IX_catalog_product_images_BatchId");

            migrationBuilder.RenameIndex(
                name: "IX_product_image_set_mappings_UseSetId",
                schema: "catalog",
                table: "catalog_product_image_set_mappings",
                newName: "IX_catalog_product_image_set_mappings_UseSetId");

            migrationBuilder.RenameIndex(
                name: "IX_product_image_set_mappings_ProductId_ForSetId",
                schema: "catalog",
                table: "catalog_product_image_set_mappings",
                newName: "IX_catalog_product_image_set_mappings_ProductId_ForSetId");

            migrationBuilder.RenameIndex(
                name: "IX_product_image_set_mappings_ForSetId",
                schema: "catalog",
                table: "catalog_product_image_set_mappings",
                newName: "IX_catalog_product_image_set_mappings_ForSetId");

            migrationBuilder.RenameIndex(
                name: "IX_product_groups_Code",
                schema: "catalog",
                table: "catalog_product_groups",
                newName: "IX_catalog_product_groups_Code");

            migrationBuilder.RenameIndex(
                name: "IX_product_group_axis_sub_attributes_SubAttributeTypeId",
                schema: "catalog",
                table: "catalog_product_group_axis_sub_attributes",
                newName: "IX_catalog_product_group_axis_sub_attributes_SubAttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_product_group_axis_sub_attributes_ProductGroupId_AxisAttrib~",
                schema: "catalog",
                table: "catalog_product_group_axis_sub_attributes",
                newName: "IX_catalog_product_group_axis_sub_attributes_ProductGroupId_Ax~");

            migrationBuilder.RenameIndex(
                name: "IX_product_group_axis_sub_attributes_AxisAttributeTypeId",
                schema: "catalog",
                table: "catalog_product_group_axis_sub_attributes",
                newName: "IX_catalog_product_group_axis_sub_attributes_AxisAttributeType~");

            migrationBuilder.RenameIndex(
                name: "IX_product_group_attributes_ProductGroupId_AttributeTypeId",
                schema: "catalog",
                table: "catalog_product_group_attributes",
                newName: "IX_catalog_product_group_attributes_ProductGroupId_AttributeTy~");

            migrationBuilder.RenameIndex(
                name: "IX_product_group_attributes_AttributeTypeId",
                schema: "catalog",
                table: "catalog_product_group_attributes",
                newName: "IX_catalog_product_group_attributes_AttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_product_axis_sub_attribute_values_SubAttributeTypeId",
                schema: "catalog",
                table: "catalog_product_axis_sub_attribute_values",
                newName: "IX_catalog_product_axis_sub_attribute_values_SubAttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_product_axis_sub_attribute_values_ProductId_AttributeValueI~",
                schema: "catalog",
                table: "catalog_product_axis_sub_attribute_values",
                newName: "IX_catalog_product_axis_sub_attribute_values_ProductId_Attribu~");

            migrationBuilder.RenameIndex(
                name: "IX_product_axis_sub_attribute_values_AttributeValueId",
                schema: "catalog",
                table: "catalog_product_axis_sub_attribute_values",
                newName: "IX_catalog_product_axis_sub_attribute_values_AttributeValueId");

            migrationBuilder.RenameIndex(
                name: "IX_product_attributes_ProductId_AttributeTypeId",
                schema: "catalog",
                table: "catalog_product_attributes",
                newName: "IX_catalog_product_attributes_ProductId_AttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_product_attributes_AttributeValueId",
                schema: "catalog",
                table: "catalog_product_attributes",
                newName: "IX_catalog_product_attributes_AttributeValueId");

            migrationBuilder.RenameIndex(
                name: "IX_product_attributes_AttributeTypeId",
                schema: "catalog",
                table: "catalog_product_attributes",
                newName: "IX_catalog_product_attributes_AttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_image_sets_FallbackSetId",
                schema: "catalog",
                table: "catalog_image_sets",
                newName: "IX_catalog_image_sets_FallbackSetId");

            migrationBuilder.RenameIndex(
                name: "IX_image_sets_Code",
                schema: "catalog",
                table: "catalog_image_sets",
                newName: "IX_catalog_image_sets_Code");

            migrationBuilder.RenameIndex(
                name: "IX_firm_platform_variants_VariantId",
                schema: "catalog",
                table: "catalog_firm_platform_variants",
                newName: "IX_catalog_firm_platform_variants_VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_firm_platform_variants_FirmPlatformId_VariantId",
                schema: "catalog",
                table: "catalog_firm_platform_variants",
                newName: "IX_catalog_firm_platform_variants_FirmPlatformId_VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_firm_platform_products_ProductId",
                schema: "catalog",
                table: "catalog_firm_platform_products",
                newName: "IX_catalog_firm_platform_products_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_firm_platform_products_FirmPlatformId_ProductId",
                schema: "catalog",
                table: "catalog_firm_platform_products",
                newName: "IX_catalog_firm_platform_products_FirmPlatformId_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_attribute_values_AttributeTypeId",
                schema: "catalog",
                table: "catalog_attribute_values",
                newName: "IX_catalog_attribute_values_AttributeTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_attribute_types_Code",
                schema: "catalog",
                table: "catalog_attribute_types",
                newName: "IX_catalog_attribute_types_Code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_variant_price_history",
                schema: "catalog",
                table: "catalog_variant_price_history",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_settings",
                schema: "catalog",
                table: "catalog_settings",
                column: "Key");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_products",
                schema: "catalog",
                table: "catalog_products",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_product_videos",
                schema: "catalog",
                table: "catalog_product_videos",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_product_variants",
                schema: "catalog",
                table: "catalog_product_variants",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_product_variant_images",
                schema: "catalog",
                table: "catalog_product_variant_images",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_product_variant_attributes",
                schema: "catalog",
                table: "catalog_product_variant_attributes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_product_units",
                schema: "catalog",
                table: "catalog_product_units",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_product_price_history",
                schema: "catalog",
                table: "catalog_product_price_history",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_product_images",
                schema: "catalog",
                table: "catalog_product_images",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_product_image_set_mappings",
                schema: "catalog",
                table: "catalog_product_image_set_mappings",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_product_groups",
                schema: "catalog",
                table: "catalog_product_groups",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_product_group_axis_sub_attributes",
                schema: "catalog",
                table: "catalog_product_group_axis_sub_attributes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_product_group_attributes",
                schema: "catalog",
                table: "catalog_product_group_attributes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_product_axis_sub_attribute_values",
                schema: "catalog",
                table: "catalog_product_axis_sub_attribute_values",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_product_attributes",
                schema: "catalog",
                table: "catalog_product_attributes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_image_sets",
                schema: "catalog",
                table: "catalog_image_sets",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_firm_platform_variants",
                schema: "catalog",
                table: "catalog_firm_platform_variants",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_firm_platform_products",
                schema: "catalog",
                table: "catalog_firm_platform_products",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_attribute_values",
                schema: "catalog",
                table: "catalog_attribute_values",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_catalog_attribute_types",
                schema: "catalog",
                table: "catalog_attribute_types",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_attribute_values_catalog_attribute_types_AttributeT~",
                schema: "catalog",
                table: "catalog_attribute_values",
                column: "AttributeTypeId",
                principalSchema: "catalog",
                principalTable: "catalog_attribute_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_firm_platform_products_catalog_products_ProductId",
                schema: "catalog",
                table: "catalog_firm_platform_products",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "catalog_products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_firm_platform_variants_catalog_product_variants_Var~",
                schema: "catalog",
                table: "catalog_firm_platform_variants",
                column: "VariantId",
                principalSchema: "catalog",
                principalTable: "catalog_product_variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_image_sets_catalog_image_sets_FallbackSetId",
                schema: "catalog",
                table: "catalog_image_sets",
                column: "FallbackSetId",
                principalSchema: "catalog",
                principalTable: "catalog_image_sets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_attributes_catalog_attribute_types_Attribut~",
                schema: "catalog",
                table: "catalog_product_attributes",
                column: "AttributeTypeId",
                principalSchema: "catalog",
                principalTable: "catalog_attribute_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_attributes_catalog_attribute_values_Attribu~",
                schema: "catalog",
                table: "catalog_product_attributes",
                column: "AttributeValueId",
                principalSchema: "catalog",
                principalTable: "catalog_attribute_values",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_attributes_catalog_products_ProductId",
                schema: "catalog",
                table: "catalog_product_attributes",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "catalog_products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_axis_sub_attribute_values_catalog_attribute~",
                schema: "catalog",
                table: "catalog_product_axis_sub_attribute_values",
                column: "AttributeValueId",
                principalSchema: "catalog",
                principalTable: "catalog_attribute_values",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_axis_sub_attribute_values_catalog_attribut~1",
                schema: "catalog",
                table: "catalog_product_axis_sub_attribute_values",
                column: "SubAttributeTypeId",
                principalSchema: "catalog",
                principalTable: "catalog_attribute_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_axis_sub_attribute_values_catalog_products_~",
                schema: "catalog",
                table: "catalog_product_axis_sub_attribute_values",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "catalog_products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_group_attributes_catalog_attribute_types_At~",
                schema: "catalog",
                table: "catalog_product_group_attributes",
                column: "AttributeTypeId",
                principalSchema: "catalog",
                principalTable: "catalog_attribute_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_group_attributes_catalog_product_groups_Pro~",
                schema: "catalog",
                table: "catalog_product_group_attributes",
                column: "ProductGroupId",
                principalSchema: "catalog",
                principalTable: "catalog_product_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_group_axis_sub_attributes_catalog_attribute~",
                schema: "catalog",
                table: "catalog_product_group_axis_sub_attributes",
                column: "AxisAttributeTypeId",
                principalSchema: "catalog",
                principalTable: "catalog_attribute_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_group_axis_sub_attributes_catalog_attribut~1",
                schema: "catalog",
                table: "catalog_product_group_axis_sub_attributes",
                column: "SubAttributeTypeId",
                principalSchema: "catalog",
                principalTable: "catalog_attribute_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_group_axis_sub_attributes_catalog_product_g~",
                schema: "catalog",
                table: "catalog_product_group_axis_sub_attributes",
                column: "ProductGroupId",
                principalSchema: "catalog",
                principalTable: "catalog_product_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_image_set_mappings_catalog_image_sets_ForSe~",
                schema: "catalog",
                table: "catalog_product_image_set_mappings",
                column: "ForSetId",
                principalSchema: "catalog",
                principalTable: "catalog_image_sets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_image_set_mappings_catalog_image_sets_UseSe~",
                schema: "catalog",
                table: "catalog_product_image_set_mappings",
                column: "UseSetId",
                principalSchema: "catalog",
                principalTable: "catalog_image_sets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_image_set_mappings_catalog_products_Product~",
                schema: "catalog",
                table: "catalog_product_image_set_mappings",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "catalog_products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_images_catalog_image_sets_ImageSetId",
                schema: "catalog",
                table: "catalog_product_images",
                column: "ImageSetId",
                principalSchema: "catalog",
                principalTable: "catalog_image_sets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_images_catalog_product_variants_VariantId",
                schema: "catalog",
                table: "catalog_product_images",
                column: "VariantId",
                principalSchema: "catalog",
                principalTable: "catalog_product_variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_images_catalog_products_ProductId",
                schema: "catalog",
                table: "catalog_product_images",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "catalog_products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_price_history_catalog_products_ProductId",
                schema: "catalog",
                table: "catalog_product_price_history",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "catalog_products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_units_catalog_product_variants_VariantId",
                schema: "catalog",
                table: "catalog_product_units",
                column: "VariantId",
                principalSchema: "catalog",
                principalTable: "catalog_product_variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_variant_attributes_catalog_attribute_types_~",
                schema: "catalog",
                table: "catalog_product_variant_attributes",
                column: "AttributeTypeId",
                principalSchema: "catalog",
                principalTable: "catalog_attribute_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_variant_attributes_catalog_attribute_values~",
                schema: "catalog",
                table: "catalog_product_variant_attributes",
                column: "AttributeValueId",
                principalSchema: "catalog",
                principalTable: "catalog_attribute_values",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_variant_attributes_catalog_product_variants~",
                schema: "catalog",
                table: "catalog_product_variant_attributes",
                column: "VariantId",
                principalSchema: "catalog",
                principalTable: "catalog_product_variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_variant_images_catalog_product_variants_Var~",
                schema: "catalog",
                table: "catalog_product_variant_images",
                column: "VariantId",
                principalSchema: "catalog",
                principalTable: "catalog_product_variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_variants_catalog_products_ProductId",
                schema: "catalog",
                table: "catalog_product_variants",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "catalog_products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_videos_catalog_image_sets_ImageSetId",
                schema: "catalog",
                table: "catalog_product_videos",
                column: "ImageSetId",
                principalSchema: "catalog",
                principalTable: "catalog_image_sets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_videos_catalog_products_ProductId",
                schema: "catalog",
                table: "catalog_product_videos",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "catalog_products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_products_catalog_product_groups_ProductGroupId",
                schema: "catalog",
                table: "catalog_products",
                column: "ProductGroupId",
                principalSchema: "catalog",
                principalTable: "catalog_product_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_variant_price_history_catalog_product_variants_Vari~",
                schema: "catalog",
                table: "catalog_variant_price_history",
                column: "VariantId",
                principalSchema: "catalog",
                principalTable: "catalog_product_variants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
