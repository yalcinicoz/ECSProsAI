using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderGifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ord_order_gifts",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    GiftReason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    AddedAtStage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AddedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ShowOnInvoice = table.Column<bool>(type: "boolean", nullable: false),
                    InvoiceDescription = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UnitValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_ord_order_gifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_order_gifts_ord_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "order",
                        principalTable: "ord_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ord_order_gifts_OrderId",
                schema: "order",
                table: "ord_order_gifts",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ord_order_gifts",
                schema: "order");
        }
    }
}
