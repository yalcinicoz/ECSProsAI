using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FavoriteUniqueIndexWithColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_favorites_FirmPlatformId_MemberId_ProductCode",
                schema: "storefront",
                table: "favorites");

            migrationBuilder.CreateIndex(
                name: "IX_favorites_FirmPlatformId_MemberId_ProductCode_ColorValueId",
                schema: "storefront",
                table: "favorites",
                columns: new[] { "FirmPlatformId", "MemberId", "ProductCode", "ColorValueId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_favorites_FirmPlatformId_MemberId_ProductCode_ColorValueId",
                schema: "storefront",
                table: "favorites");

            migrationBuilder.CreateIndex(
                name: "IX_favorites_FirmPlatformId_MemberId_ProductCode",
                schema: "storefront",
                table: "favorites",
                columns: new[] { "FirmPlatformId", "MemberId", "ProductCode" },
                unique: true);
        }
    }
}
