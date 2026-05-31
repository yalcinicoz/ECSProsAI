using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_catalog_product_variants_Barcode",
                schema: "catalog",
                table: "catalog_product_variants");

            migrationBuilder.CreateTable(
                name: "catalog_settings",
                schema: "catalog",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_settings", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_variants_Barcode",
                schema: "catalog",
                table: "catalog_product_variants",
                column: "Barcode",
                unique: true,
                filter: "\"Barcode\" IS NOT NULL AND \"Barcode\" <> ''");

            // Seed default barcode sequence settings
            migrationBuilder.InsertData(
                schema: "catalog",
                table: "catalog_settings",
                columns: new[] { "Key", "Value", "UpdatedAt" },
                values: new object[,]
                {
                    { "barcode_sequence", "1", DateTime.UtcNow },
                    { "barcode_prefix", "", DateTime.UtcNow }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_settings",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_catalog_product_variants_Barcode",
                schema: "catalog",
                table: "catalog_product_variants");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_variants_Barcode",
                schema: "catalog",
                table: "catalog_product_variants",
                column: "Barcode",
                unique: true,
                filter: "barcode IS NOT NULL AND barcode <> ''");
        }
    }
}
