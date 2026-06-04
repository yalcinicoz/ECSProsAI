using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelCategorySlugUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_channel_categories_FirmPlatformId_Slug",
                schema: "storefront",
                table: "channel_categories",
                columns: new[] { "FirmPlatformId", "Slug" },
                unique: true,
                filter: "\"Slug\" IS NOT NULL AND \"Slug\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_channel_categories_FirmPlatformId_Slug",
                schema: "storefront",
                table: "channel_categories");
        }
    }
}
