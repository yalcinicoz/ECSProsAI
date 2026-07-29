using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierSnapshotAndPackageLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PackageId",
                schema: "order",
                table: "ord_shipments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                schema: "order",
                table: "ord_order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PackageId",
                schema: "order",
                table: "ord_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ord_shipments_PackageId",
                schema: "order",
                table: "ord_shipments",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_invoices_PackageId",
                schema: "order",
                table: "ord_invoices",
                column: "PackageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ord_shipments_PackageId",
                schema: "order",
                table: "ord_shipments");

            migrationBuilder.DropIndex(
                name: "IX_ord_invoices_PackageId",
                schema: "order",
                table: "ord_invoices");

            migrationBuilder.DropColumn(
                name: "PackageId",
                schema: "order",
                table: "ord_shipments");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                schema: "order",
                table: "ord_order_items");

            migrationBuilder.DropColumn(
                name: "PackageId",
                schema: "order",
                table: "ord_invoices");
        }
    }
}
