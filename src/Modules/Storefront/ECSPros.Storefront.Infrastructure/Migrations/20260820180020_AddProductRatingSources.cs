using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductRatingSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_rating_sources",
                schema: "storefront",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExternalProductId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AverageRating = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ReviewCount = table.Column<int>(type: "integer", nullable: false),
                    ExternalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_product_rating_sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "product_review_display_settings",
                schema: "storefront",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateChannels = table.Column<List<string>>(type: "text[]", nullable: false),
                    ListChannels = table.Column<List<string>>(type: "text[]", nullable: false),
                    ShowReviewPhotos = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_product_review_display_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_rating_sources_FirmPlatformId_Channel",
                schema: "storefront",
                table: "product_rating_sources",
                columns: new[] { "FirmPlatformId", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_product_rating_sources_FirmPlatformId_ProductCode_Channel",
                schema: "storefront",
                table: "product_rating_sources",
                columns: new[] { "FirmPlatformId", "ProductCode", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_review_display_settings_FirmPlatformId",
                schema: "storefront",
                table: "product_review_display_settings",
                column: "FirmPlatformId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_rating_sources",
                schema: "storefront");

            migrationBuilder.DropTable(
                name: "product_review_display_settings",
                schema: "storefront");
        }
    }
}
