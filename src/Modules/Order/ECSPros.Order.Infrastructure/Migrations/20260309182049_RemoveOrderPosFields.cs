using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOrderPosFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPosSale",
                schema: "order",
                table: "ord_orders");

            migrationBuilder.DropColumn(
                name: "PosRegisterId",
                schema: "order",
                table: "ord_orders");

            migrationBuilder.DropColumn(
                name: "PosSessionId",
                schema: "order",
                table: "ord_orders");

            migrationBuilder.DropColumn(
                name: "ReceiptNumber",
                schema: "order",
                table: "ord_orders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPosSale",
                schema: "order",
                table: "ord_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PosRegisterId",
                schema: "order",
                table: "ord_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PosSessionId",
                schema: "order",
                table: "ord_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptNumber",
                schema: "order",
                table: "ord_orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
