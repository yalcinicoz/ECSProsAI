using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelVariantSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                schema: "storefront",
                table: "channel_variants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_channel_variants_FirmPlatformId_Slug",
                schema: "storefront",
                table: "channel_variants",
                columns: new[] { "FirmPlatformId", "Slug" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_channel_variants_FirmPlatformId_Slug",
                schema: "storefront",
                table: "channel_variants");

            migrationBuilder.DropColumn(
                name: "Slug",
                schema: "storefront",
                table: "channel_variants");
        }
    }
}
