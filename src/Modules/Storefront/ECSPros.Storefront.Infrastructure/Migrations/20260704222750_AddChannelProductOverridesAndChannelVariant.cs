using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelProductOverridesAndChannelVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "NameI18n",
                schema: "storefront",
                table: "channel_products",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "ShortDescriptionI18n",
                schema: "storefront",
                table: "channel_products",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "channel_variants",
                schema: "storefront",
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
                    table.PrimaryKey("PK_channel_variants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_channel_variants_FirmPlatformId_VariantId",
                schema: "storefront",
                table: "channel_variants",
                columns: new[] { "FirmPlatformId", "VariantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_variants",
                schema: "storefront");

            migrationBuilder.DropColumn(
                name: "NameI18n",
                schema: "storefront",
                table: "channel_products");

            migrationBuilder.DropColumn(
                name: "ShortDescriptionI18n",
                schema: "storefront",
                table: "channel_products");
        }
    }
}
