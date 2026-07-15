using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelProductSaleStop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SaleStoppedFrom",
                schema: "storefront",
                table: "channel_products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SaleStoppedUntil",
                schema: "storefront",
                table: "channel_products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_channel_products_FirmPlatformId_IsActive",
                schema: "storefront",
                table: "channel_products",
                columns: new[] { "FirmPlatformId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_channel_products_FirmPlatformId_IsActive",
                schema: "storefront",
                table: "channel_products");

            migrationBuilder.DropColumn(
                name: "SaleStoppedFrom",
                schema: "storefront",
                table: "channel_products");

            migrationBuilder.DropColumn(
                name: "SaleStoppedUntil",
                schema: "storefront",
                table: "channel_products");
        }
    }
}
