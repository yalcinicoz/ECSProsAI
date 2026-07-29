using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceProductReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "marketplace_product_attribute_values",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Marketplace = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: true),
                    MpCategoryExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MpAttributeExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MpAttributeName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ValueExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ValueCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ValueText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_marketplace_product_attribute_values", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_product_category_overrides",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Marketplace = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CategoryPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_marketplace_product_category_overrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_product_readiness",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Marketplace = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReasonsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResolvedCategoryExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResolvedCategoryPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ComputedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_marketplace_product_readiness", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_product_attribute_values_Marketplace_ProductId",
                schema: "integration",
                table: "marketplace_product_attribute_values",
                columns: new[] { "Marketplace", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_product_attribute_values_Marketplace_ProductId_~",
                schema: "integration",
                table: "marketplace_product_attribute_values",
                columns: new[] { "Marketplace", "ProductId", "MpCategoryExternalId", "MpAttributeExternalId", "FirmPlatformId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_product_category_overrides_Marketplace_ProductI~",
                schema: "integration",
                table: "marketplace_product_category_overrides",
                columns: new[] { "Marketplace", "ProductId", "FirmPlatformId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_product_readiness_Marketplace_ProductId_FirmPla~",
                schema: "integration",
                table: "marketplace_product_readiness",
                columns: new[] { "Marketplace", "ProductId", "FirmPlatformId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_product_readiness_Marketplace_Status",
                schema: "integration",
                table: "marketplace_product_readiness",
                columns: new[] { "Marketplace", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "marketplace_product_attribute_values",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "marketplace_product_category_overrides",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "marketplace_product_readiness",
                schema: "integration");
        }
    }
}
