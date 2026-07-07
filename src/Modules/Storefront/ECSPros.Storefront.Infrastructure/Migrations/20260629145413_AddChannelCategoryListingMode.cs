using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelCategoryListingMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShowcaseProductId",
                schema: "storefront",
                table: "channel_category_groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ListingMode",
                schema: "storefront",
                table: "channel_categories",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "color");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowcaseProductId",
                schema: "storefront",
                table: "channel_category_groups");

            migrationBuilder.DropColumn(
                name: "ListingMode",
                schema: "storefront",
                table: "channel_categories");
        }
    }
}
