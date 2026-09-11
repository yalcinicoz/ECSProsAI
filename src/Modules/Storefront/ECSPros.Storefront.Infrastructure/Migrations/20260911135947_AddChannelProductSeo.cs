using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelProductSeo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "MetaDescriptionI18n",
                schema: "storefront",
                table: "channel_products",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "MetaTitleI18n",
                schema: "storefront",
                table: "channel_products",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetaDescriptionI18n",
                schema: "storefront",
                table: "channel_products");

            migrationBuilder.DropColumn(
                name: "MetaTitleI18n",
                schema: "storefront",
                table: "channel_products");
        }
    }
}
