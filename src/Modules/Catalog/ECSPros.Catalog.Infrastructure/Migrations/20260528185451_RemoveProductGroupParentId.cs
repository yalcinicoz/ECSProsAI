using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProductGroupParentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_catalog_product_groups_catalog_product_groups_ParentId",
                schema: "catalog",
                table: "catalog_product_groups");

            migrationBuilder.DropIndex(
                name: "IX_catalog_product_groups_ParentId",
                schema: "catalog",
                table: "catalog_product_groups");

            migrationBuilder.DropColumn(
                name: "ParentId",
                schema: "catalog",
                table: "catalog_product_groups");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                schema: "catalog",
                table: "catalog_product_groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_groups_ParentId",
                schema: "catalog",
                table: "catalog_product_groups",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_product_groups_catalog_product_groups_ParentId",
                schema: "catalog",
                table: "catalog_product_groups",
                column: "ParentId",
                principalSchema: "catalog",
                principalTable: "catalog_product_groups",
                principalColumn: "Id");
        }
    }
}
