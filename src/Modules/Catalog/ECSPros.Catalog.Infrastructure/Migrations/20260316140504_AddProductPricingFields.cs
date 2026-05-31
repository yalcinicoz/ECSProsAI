using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPricingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaseCost",
                schema: "catalog",
                table: "catalog_products",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BasePrice",
                schema: "catalog",
                table: "catalog_products",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "DescriptionI18n",
                schema: "catalog",
                table: "catalog_products",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaxRate",
                schema: "catalog",
                table: "catalog_products",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseCost",
                schema: "catalog",
                table: "catalog_products");

            migrationBuilder.DropColumn(
                name: "BasePrice",
                schema: "catalog",
                table: "catalog_products");

            migrationBuilder.DropColumn(
                name: "DescriptionI18n",
                schema: "catalog",
                table: "catalog_products");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                schema: "catalog",
                table: "catalog_products");
        }
    }
}
