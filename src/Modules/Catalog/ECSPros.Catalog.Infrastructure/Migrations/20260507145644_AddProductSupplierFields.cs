using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSupplierFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                schema: "catalog",
                table: "catalog_products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierProductCode",
                schema: "catalog",
                table: "catalog_products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupplierId",
                schema: "catalog",
                table: "catalog_products");

            migrationBuilder.DropColumn(
                name: "SupplierProductCode",
                schema: "catalog",
                table: "catalog_products");
        }
    }
}
