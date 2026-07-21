using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_submissions",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierProductCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GroupCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    VariantCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApiClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewNote = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_submissions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_products_SupplierId",
                schema: "catalog",
                table: "products",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_products_SupplierId_SupplierProductCode",
                schema: "catalog",
                table: "products",
                columns: new[] { "SupplierId", "SupplierProductCode" },
                unique: true,
                filter: "\"SupplierId\" IS NOT NULL AND \"SupplierProductCode\" IS NOT NULL AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_product_submissions_SupplierId_Status",
                schema: "catalog",
                table: "product_submissions",
                columns: new[] { "SupplierId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_product_submissions_SupplierId_SupplierProductCode",
                schema: "catalog",
                table: "product_submissions",
                columns: new[] { "SupplierId", "SupplierProductCode" },
                unique: true,
                filter: "\"Status\" = 'pending' AND NOT \"IsDeleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_submissions",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_products_SupplierId",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_SupplierId_SupplierProductCode",
                schema: "catalog",
                table: "products");
        }
    }
}
