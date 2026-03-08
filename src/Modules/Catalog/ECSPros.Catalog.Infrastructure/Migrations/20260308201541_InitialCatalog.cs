using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "catalog_attribute_types",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    DataType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_catalog_attribute_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "catalog_categories",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    FillType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FilterRules = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_catalog_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_categories_catalog_categories_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "catalog",
                        principalTable: "catalog_categories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "catalog_product_groups",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_catalog_product_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_product_groups_catalog_product_groups_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "catalog",
                        principalTable: "catalog_product_groups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "catalog_attribute_values",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    ExtraData = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_catalog_attribute_values", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_attribute_values_catalog_attribute_types_AttributeT~",
                        column: x => x.AttributeTypeId,
                        principalSchema: "catalog",
                        principalTable: "catalog_attribute_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "catalog_product_group_attributes",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsVariant = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_catalog_product_group_attributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_product_group_attributes_catalog_attribute_types_At~",
                        column: x => x.AttributeTypeId,
                        principalSchema: "catalog",
                        principalTable: "catalog_attribute_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_catalog_product_group_attributes_catalog_product_groups_Pro~",
                        column: x => x.ProductGroupId,
                        principalSchema: "catalog",
                        principalTable: "catalog_product_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "catalog_products",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    ShortDescriptionI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_catalog_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_products_catalog_product_groups_ProductGroupId",
                        column: x => x.ProductGroupId,
                        principalSchema: "catalog",
                        principalTable: "catalog_product_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "catalog_category_products",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_catalog_category_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_category_products_catalog_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "catalog",
                        principalTable: "catalog_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_catalog_category_products_catalog_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "catalog_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "catalog_firm_platform_products",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    ShortDescriptionI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_catalog_firm_platform_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_firm_platform_products_catalog_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "catalog_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "catalog_product_attributes",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeValueId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomValue = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
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
                    table.PrimaryKey("PK_catalog_product_attributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_product_attributes_catalog_attribute_types_Attribut~",
                        column: x => x.AttributeTypeId,
                        principalSchema: "catalog",
                        principalTable: "catalog_attribute_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_catalog_product_attributes_catalog_attribute_values_Attribu~",
                        column: x => x.AttributeValueId,
                        principalSchema: "catalog",
                        principalTable: "catalog_attribute_values",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_catalog_product_attributes_catalog_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "catalog_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "catalog_product_variants",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BaseCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_catalog_product_variants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_product_variants_catalog_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "catalog_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "catalog_firm_platform_variants",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PriceMultiplier = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CompareAtPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_catalog_firm_platform_variants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_firm_platform_variants_catalog_product_variants_Var~",
                        column: x => x.VariantId,
                        principalSchema: "catalog",
                        principalTable: "catalog_product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "catalog_product_units",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UnitNameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    PiecesPerUnit = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    MinOrderQuantity = table.Column<int>(type: "integer", nullable: false),
                    PriceMultiplier = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
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
                    table.PrimaryKey("PK_catalog_product_units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_product_units_catalog_product_variants_VariantId",
                        column: x => x.VariantId,
                        principalSchema: "catalog",
                        principalTable: "catalog_product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "catalog_product_variant_attributes",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeValueId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_catalog_product_variant_attributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_product_variant_attributes_catalog_attribute_types_~",
                        column: x => x.AttributeTypeId,
                        principalSchema: "catalog",
                        principalTable: "catalog_attribute_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_catalog_product_variant_attributes_catalog_attribute_values~",
                        column: x => x.AttributeValueId,
                        principalSchema: "catalog",
                        principalTable: "catalog_attribute_values",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_catalog_product_variant_attributes_catalog_product_variants~",
                        column: x => x.VariantId,
                        principalSchema: "catalog",
                        principalTable: "catalog_product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "catalog_product_variant_images",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsMain = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_catalog_product_variant_images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_product_variant_images_catalog_product_variants_Var~",
                        column: x => x.VariantId,
                        principalSchema: "catalog",
                        principalTable: "catalog_product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "catalog_variant_price_history",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: true),
                    PriceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OldValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NewValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_variant_price_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_variant_price_history_catalog_product_variants_Vari~",
                        column: x => x.VariantId,
                        principalSchema: "catalog",
                        principalTable: "catalog_product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_attribute_types_Code",
                schema: "catalog",
                table: "catalog_attribute_types",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_attribute_values_AttributeTypeId_Code",
                schema: "catalog",
                table: "catalog_attribute_values",
                columns: new[] { "AttributeTypeId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_categories_Code",
                schema: "catalog",
                table: "catalog_categories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_categories_ParentId",
                schema: "catalog",
                table: "catalog_categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_category_products_CategoryId_ProductId",
                schema: "catalog",
                table: "catalog_category_products",
                columns: new[] { "CategoryId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_category_products_ProductId",
                schema: "catalog",
                table: "catalog_category_products",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_firm_platform_products_FirmPlatformId_ProductId",
                schema: "catalog",
                table: "catalog_firm_platform_products",
                columns: new[] { "FirmPlatformId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_firm_platform_products_ProductId",
                schema: "catalog",
                table: "catalog_firm_platform_products",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_firm_platform_variants_FirmPlatformId_VariantId",
                schema: "catalog",
                table: "catalog_firm_platform_variants",
                columns: new[] { "FirmPlatformId", "VariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_firm_platform_variants_VariantId",
                schema: "catalog",
                table: "catalog_firm_platform_variants",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_attributes_AttributeTypeId",
                schema: "catalog",
                table: "catalog_product_attributes",
                column: "AttributeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_attributes_AttributeValueId",
                schema: "catalog",
                table: "catalog_product_attributes",
                column: "AttributeValueId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_attributes_ProductId_AttributeTypeId",
                schema: "catalog",
                table: "catalog_product_attributes",
                columns: new[] { "ProductId", "AttributeTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_group_attributes_AttributeTypeId",
                schema: "catalog",
                table: "catalog_product_group_attributes",
                column: "AttributeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_group_attributes_ProductGroupId_AttributeTy~",
                schema: "catalog",
                table: "catalog_product_group_attributes",
                columns: new[] { "ProductGroupId", "AttributeTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_groups_Code",
                schema: "catalog",
                table: "catalog_product_groups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_groups_ParentId",
                schema: "catalog",
                table: "catalog_product_groups",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_units_VariantId_UnitType",
                schema: "catalog",
                table: "catalog_product_units",
                columns: new[] { "VariantId", "UnitType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_variant_attributes_AttributeTypeId",
                schema: "catalog",
                table: "catalog_product_variant_attributes",
                column: "AttributeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_variant_attributes_AttributeValueId",
                schema: "catalog",
                table: "catalog_product_variant_attributes",
                column: "AttributeValueId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_variant_attributes_VariantId_AttributeTypeId",
                schema: "catalog",
                table: "catalog_product_variant_attributes",
                columns: new[] { "VariantId", "AttributeTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_variant_images_VariantId",
                schema: "catalog",
                table: "catalog_product_variant_images",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_variants_ProductId",
                schema: "catalog",
                table: "catalog_product_variants",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_variants_Sku",
                schema: "catalog",
                table: "catalog_product_variants",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_products_Code",
                schema: "catalog",
                table: "catalog_products",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_products_ProductGroupId",
                schema: "catalog",
                table: "catalog_products",
                column: "ProductGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_variant_price_history_VariantId",
                schema: "catalog",
                table: "catalog_variant_price_history",
                column: "VariantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_category_products",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_firm_platform_products",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_firm_platform_variants",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_product_attributes",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_product_group_attributes",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_product_units",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_product_variant_attributes",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_product_variant_images",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_variant_price_history",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_categories",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_attribute_values",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_product_variants",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_attribute_types",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_products",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_product_groups",
                schema: "catalog");
        }
    }
}
