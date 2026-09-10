using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnFlowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedAt",
                schema: "order",
                table: "ord_shipments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundNotApplicableReason",
                schema: "order",
                table: "ord_returns",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StockAlreadyIn",
                schema: "order",
                table: "ord_return_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // İade planı R1: yeni sistemde açılmış müşteri iadeleri ("return"/"refund") sözlüğe çekilir; legacy_type_N
            // aktarım kayıtları bu planın DIŞINDA (aktarım düzeltmesi sonra).
            migrationBuilder.Sql("""UPDATE "order".ord_returns SET "ReturnType" = 'customer' WHERE "ReturnType" IN ('return', 'refund');""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReturnedAt",
                schema: "order",
                table: "ord_shipments");

            migrationBuilder.DropColumn(
                name: "RefundNotApplicableReason",
                schema: "order",
                table: "ord_returns");

            migrationBuilder.DropColumn(
                name: "StockAlreadyIn",
                schema: "order",
                table: "ord_return_items");
        }
    }
}
