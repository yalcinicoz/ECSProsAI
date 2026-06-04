using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CategoryId",
                schema: "storefront",
                table: "nav_nodes",
                newName: "ChannelCategoryId");

            // Eski global Category ID'leri yeni channel_categories tablosunda yok; NULL'a çek
            migrationBuilder.Sql(
                "UPDATE storefront.nav_nodes SET \"ChannelCategoryId\" = NULL WHERE \"ChannelCategoryId\" IS NOT NULL;");

            migrationBuilder.CreateTable(
                name: "channel_categories",
                schema: "storefront",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FillType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FilterDef = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    DisplayImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BadgeLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MetaTitleI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    MetaDescriptionI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    OgImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OgTitleI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
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
                    table.PrimaryKey("PK_channel_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_channel_categories_channel_categories_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "storefront",
                        principalTable: "channel_categories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "channel_product_groups",
                schema: "storefront",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_channel_product_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "channel_category_groups",
                schema: "storefront",
                columns: table => new
                {
                    ChannelCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductGroupId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channel_category_groups", x => new { x.ChannelCategoryId, x.ProductGroupId });
                    table.ForeignKey(
                        name: "FK_channel_category_groups_channel_categories_ChannelCategoryId",
                        column: x => x.ChannelCategoryId,
                        principalSchema: "storefront",
                        principalTable: "channel_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "channel_category_products",
                schema: "storefront",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsExcluded = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channel_category_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_channel_category_products_channel_categories_ChannelCategor~",
                        column: x => x.ChannelCategoryId,
                        principalSchema: "storefront",
                        principalTable: "channel_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_nav_nodes_ChannelCategoryId",
                schema: "storefront",
                table: "nav_nodes",
                column: "ChannelCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_channel_categories_ParentId",
                schema: "storefront",
                table: "channel_categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_channel_category_products_ChannelCategoryId_ProductId",
                schema: "storefront",
                table: "channel_category_products",
                columns: new[] { "ChannelCategoryId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_channel_product_groups_FirmPlatformId_ProductGroupId",
                schema: "storefront",
                table: "channel_product_groups",
                columns: new[] { "FirmPlatformId", "ProductGroupId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_nav_nodes_channel_categories_ChannelCategoryId",
                schema: "storefront",
                table: "nav_nodes",
                column: "ChannelCategoryId",
                principalSchema: "storefront",
                principalTable: "channel_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_nav_nodes_channel_categories_ChannelCategoryId",
                schema: "storefront",
                table: "nav_nodes");

            migrationBuilder.DropTable(
                name: "channel_category_groups",
                schema: "storefront");

            migrationBuilder.DropTable(
                name: "channel_category_products",
                schema: "storefront");

            migrationBuilder.DropTable(
                name: "channel_product_groups",
                schema: "storefront");

            migrationBuilder.DropTable(
                name: "channel_categories",
                schema: "storefront");

            migrationBuilder.DropIndex(
                name: "IX_nav_nodes_ChannelCategoryId",
                schema: "storefront",
                table: "nav_nodes");

            migrationBuilder.RenameColumn(
                name: "ChannelCategoryId",
                schema: "storefront",
                table: "nav_nodes",
                newName: "CategoryId");
        }
    }
}
