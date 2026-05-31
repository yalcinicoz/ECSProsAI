using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialStorefront : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "storefront");

            migrationBuilder.CreateTable(
                name: "channel_products",
                schema: "storefront",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_channel_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "nav_menus",
                schema: "storefront",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    MenuType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_nav_menus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "nav_nodes",
                schema: "storefront",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NavigationMenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentNavNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    NameOverrideI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BadgeLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OpenInNewTab = table.Column<bool>(type: "boolean", nullable: false),
                    NodeType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CustomUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SeoTitleI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    SeoDescriptionI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    CanonicalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OgImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OgTitleI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
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
                    table.PrimaryKey("PK_nav_nodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_nav_nodes_nav_menus_NavigationMenuId",
                        column: x => x.NavigationMenuId,
                        principalSchema: "storefront",
                        principalTable: "nav_menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_nav_nodes_nav_nodes_ParentNavNodeId",
                        column: x => x.ParentNavNodeId,
                        principalSchema: "storefront",
                        principalTable: "nav_nodes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_channel_products_FirmPlatformId_ProductId",
                schema: "storefront",
                table: "channel_products",
                columns: new[] { "FirmPlatformId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nav_menus_FirmPlatformId_Code",
                schema: "storefront",
                table: "nav_menus",
                columns: new[] { "FirmPlatformId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nav_nodes_NavigationMenuId",
                schema: "storefront",
                table: "nav_nodes",
                column: "NavigationMenuId");

            migrationBuilder.CreateIndex(
                name: "IX_nav_nodes_ParentNavNodeId",
                schema: "storefront",
                table: "nav_nodes",
                column: "ParentNavNodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_products",
                schema: "storefront");

            migrationBuilder.DropTable(
                name: "nav_nodes",
                schema: "storefront");

            migrationBuilder.DropTable(
                name: "nav_menus",
                schema: "storefront");
        }
    }
}
