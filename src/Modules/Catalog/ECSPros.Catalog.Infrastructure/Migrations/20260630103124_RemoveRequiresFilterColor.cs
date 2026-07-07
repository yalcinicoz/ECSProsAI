using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRequiresFilterColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiresFilterColor",
                schema: "catalog",
                table: "catalog_attribute_types");

            migrationBuilder.AddColumn<string>(
                name: "CdnBaseUrl",
                schema: "catalog",
                table: "catalog_image_sets",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CdnBaseUrl",
                schema: "catalog",
                table: "catalog_image_sets");

            migrationBuilder.AddColumn<bool>(
                name: "RequiresFilterColor",
                schema: "catalog",
                table: "catalog_attribute_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
