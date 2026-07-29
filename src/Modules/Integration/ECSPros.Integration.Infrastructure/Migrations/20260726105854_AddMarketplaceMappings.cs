using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "marketplace_attribute_mappings",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Marketplace = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MpCategoryExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MpAttributeExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MpAttributeName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: true),
                    Strategy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AttributeTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    FixedValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StatusNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_marketplace_attribute_mappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_category_mappings",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Marketplace = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: true),
                    MappingKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TargetName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TargetPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RulesJson = table.Column<string>(type: "jsonb", nullable: true),
                    PoolJson = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StatusNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_marketplace_category_mappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_value_mappings",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Marketplace = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MpCategoryExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MpAttributeExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AttributeValueId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TargetCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TargetValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StatusNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_marketplace_value_mappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_attribute_mappings_Marketplace_MpCategoryExtern~",
                schema: "integration",
                table: "marketplace_attribute_mappings",
                columns: new[] { "Marketplace", "MpCategoryExternalId", "MpAttributeExternalId", "FirmPlatformId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_attribute_mappings_Marketplace_Status",
                schema: "integration",
                table: "marketplace_attribute_mappings",
                columns: new[] { "Marketplace", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_category_mappings_Marketplace_ProductGroupId_Fi~",
                schema: "integration",
                table: "marketplace_category_mappings",
                columns: new[] { "Marketplace", "ProductGroupId", "FirmPlatformId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_category_mappings_Marketplace_Status",
                schema: "integration",
                table: "marketplace_category_mappings",
                columns: new[] { "Marketplace", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_value_mappings_Marketplace_MpCategoryExternalId",
                schema: "integration",
                table: "marketplace_value_mappings",
                columns: new[] { "Marketplace", "MpCategoryExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_value_mappings_Marketplace_MpCategoryExternalId~",
                schema: "integration",
                table: "marketplace_value_mappings",
                columns: new[] { "Marketplace", "MpCategoryExternalId", "MpAttributeExternalId", "AttributeValueId", "FirmPlatformId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "marketplace_attribute_mappings",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "marketplace_category_mappings",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "marketplace_value_mappings",
                schema: "integration");
        }
    }
}
