using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAttributeValueCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_catalog_attribute_values_AttributeTypeId_Code",
                schema: "catalog",
                table: "catalog_attribute_values");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "catalog",
                table: "catalog_attribute_values");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_attribute_values_AttributeTypeId",
                schema: "catalog",
                table: "catalog_attribute_values",
                column: "AttributeTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_catalog_attribute_values_AttributeTypeId",
                schema: "catalog",
                table: "catalog_attribute_values");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "catalog",
                table: "catalog_attribute_values",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_attribute_values_AttributeTypeId_Code",
                schema: "catalog",
                table: "catalog_attribute_values",
                columns: new[] { "AttributeTypeId", "Code" },
                unique: true);
        }
    }
}
