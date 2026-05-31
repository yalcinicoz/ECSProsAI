using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductTagsAndSeo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "MetaDescriptionI18n",
                schema: "catalog",
                table: "catalog_products",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "MetaKeywordsI18n",
                schema: "catalog",
                table: "catalog_products",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "MetaTitleI18n",
                schema: "catalog",
                table: "catalog_products",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                schema: "catalog",
                table: "catalog_products",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Tags",
                schema: "catalog",
                table: "catalog_products",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_products_Slug",
                schema: "catalog",
                table: "catalog_products",
                column: "Slug",
                unique: true,
                filter: "\"Slug\" IS NOT NULL AND \"Slug\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_catalog_products_Slug",
                schema: "catalog",
                table: "catalog_products");

            migrationBuilder.DropColumn(
                name: "MetaDescriptionI18n",
                schema: "catalog",
                table: "catalog_products");

            migrationBuilder.DropColumn(
                name: "MetaKeywordsI18n",
                schema: "catalog",
                table: "catalog_products");

            migrationBuilder.DropColumn(
                name: "MetaTitleI18n",
                schema: "catalog",
                table: "catalog_products");

            migrationBuilder.DropColumn(
                name: "Slug",
                schema: "catalog",
                table: "catalog_products");

            migrationBuilder.DropColumn(
                name: "Tags",
                schema: "catalog",
                table: "catalog_products");
        }
    }
}
