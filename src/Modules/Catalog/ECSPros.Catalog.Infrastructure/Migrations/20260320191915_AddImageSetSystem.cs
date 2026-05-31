using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImageSetSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_image_sets",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    FallbackSetId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortPriority = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_catalog_image_sets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_image_sets_catalog_image_sets_FallbackSetId",
                        column: x => x.FallbackSetId,
                        principalSchema: "catalog",
                        principalTable: "catalog_image_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "catalog_product_image_set_mappings",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ForSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    UseSetId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_catalog_product_image_set_mappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_product_image_set_mappings_catalog_image_sets_ForSe~",
                        column: x => x.ForSetId,
                        principalSchema: "catalog",
                        principalTable: "catalog_image_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_product_image_set_mappings_catalog_image_sets_UseSe~",
                        column: x => x.UseSetId,
                        principalSchema: "catalog",
                        principalTable: "catalog_image_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_product_image_set_mappings_catalog_products_Product~",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "catalog_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "catalog_product_images",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImageSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsProductCover = table.Column<bool>(type: "boolean", nullable: false),
                    IsVariantCover = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_catalog_product_images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_product_images_catalog_image_sets_ImageSetId",
                        column: x => x.ImageSetId,
                        principalSchema: "catalog",
                        principalTable: "catalog_image_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_product_images_catalog_product_variants_VariantId",
                        column: x => x.VariantId,
                        principalSchema: "catalog",
                        principalTable: "catalog_product_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_catalog_product_images_catalog_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "catalog_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_image_sets_Code",
                schema: "catalog",
                table: "catalog_image_sets",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_image_sets_FallbackSetId",
                schema: "catalog",
                table: "catalog_image_sets",
                column: "FallbackSetId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_image_set_mappings_ForSetId",
                schema: "catalog",
                table: "catalog_product_image_set_mappings",
                column: "ForSetId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_image_set_mappings_ProductId_ForSetId",
                schema: "catalog",
                table: "catalog_product_image_set_mappings",
                columns: new[] { "ProductId", "ForSetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_image_set_mappings_UseSetId",
                schema: "catalog",
                table: "catalog_product_image_set_mappings",
                column: "UseSetId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_images_BatchId",
                schema: "catalog",
                table: "catalog_product_images",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_images_ImageSetId",
                schema: "catalog",
                table: "catalog_product_images",
                column: "ImageSetId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_images_ProductId_ImageSetId_Status",
                schema: "catalog",
                table: "catalog_product_images",
                columns: new[] { "ProductId", "ImageSetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_images_VariantId",
                schema: "catalog",
                table: "catalog_product_images",
                column: "VariantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_product_image_set_mappings",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_product_images",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_image_sets",
                schema: "catalog");
        }
    }
}
