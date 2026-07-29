using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestedCargoToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RequestedCargoIntegrationId",
                schema: "order",
                table: "ord_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedCargoName",
                schema: "order",
                table: "ord_orders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestedCargoIntegrationId",
                schema: "order",
                table: "ord_orders");

            migrationBuilder.DropColumn(
                name: "RequestedCargoName",
                schema: "order",
                table: "ord_orders");
        }
    }
}
