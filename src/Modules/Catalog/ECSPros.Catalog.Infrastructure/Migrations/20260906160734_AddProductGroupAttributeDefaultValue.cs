using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductGroupAttributeDefaultValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultAttributeValueId",
                schema: "definition",
                table: "product_group_attributes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_group_attributes_DefaultAttributeValueId",
                schema: "definition",
                table: "product_group_attributes",
                column: "DefaultAttributeValueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_product_group_attributes_DefaultAttributeValueId",
                schema: "definition",
                table: "product_group_attributes");

            migrationBuilder.DropColumn(
                name: "DefaultAttributeValueId",
                schema: "definition",
                table: "product_group_attributes");
        }
    }
}
