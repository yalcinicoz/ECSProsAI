using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderNumberSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ord_orders_OrderNumber",
                schema: "order",
                table: "ord_orders");

            migrationBuilder.AddColumn<string>(
                name: "ExternalOrderNumber",
                schema: "order",
                table: "ord_orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderNumberSource",
                schema: "order",
                table: "ord_orders",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "internal");

            migrationBuilder.CreateTable(
                name: "ord_order_number_series",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    Prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PadLength = table.Column<int>(type: "integer", nullable: false),
                    NextValue = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ord_order_number_series", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ord_orders_FirmPlatformId_OrderNumber",
                schema: "order",
                table: "ord_orders",
                columns: new[] { "FirmPlatformId", "OrderNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ord_order_number_series_FirmPlatformId",
                schema: "order",
                table: "ord_order_number_series",
                column: "FirmPlatformId",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ord_order_number_series",
                schema: "order");

            migrationBuilder.DropIndex(
                name: "IX_ord_orders_FirmPlatformId_OrderNumber",
                schema: "order",
                table: "ord_orders");

            migrationBuilder.DropColumn(
                name: "ExternalOrderNumber",
                schema: "order",
                table: "ord_orders");

            migrationBuilder.DropColumn(
                name: "OrderNumberSource",
                schema: "order",
                table: "ord_orders");

            migrationBuilder.CreateIndex(
                name: "IX_ord_orders_OrderNumber",
                schema: "order",
                table: "ord_orders",
                column: "OrderNumber",
                unique: true);
        }
    }
}
