using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceHistoryUserFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChangedByName",
                schema: "catalog",
                table: "catalog_variant_price_history",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirmPlatformCode",
                schema: "catalog",
                table: "catalog_variant_price_history",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangedByName",
                schema: "catalog",
                table: "catalog_product_price_history",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChangedByName",
                schema: "catalog",
                table: "catalog_variant_price_history");

            migrationBuilder.DropColumn(
                name: "FirmPlatformCode",
                schema: "catalog",
                table: "catalog_variant_price_history");

            migrationBuilder.DropColumn(
                name: "ChangedByName",
                schema: "catalog",
                table: "catalog_product_price_history");
        }
    }
}
