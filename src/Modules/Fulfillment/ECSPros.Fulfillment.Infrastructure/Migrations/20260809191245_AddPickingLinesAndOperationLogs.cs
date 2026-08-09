using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Fulfillment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPickingLinesAndOperationLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ful_operation_logs",
                schema: "fulfillment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    PickingPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Detail = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
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
                    table.PrimaryKey("PK_ful_operation_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ful_operation_profiles",
                schema: "fulfillment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmId = table.Column<Guid>(type: "uuid", nullable: false),
                    UseIntermediateSorting = table.Column<bool>(type: "boolean", nullable: false),
                    SingleItemFastLane = table.Column<bool>(type: "boolean", nullable: false),
                    MaxOrdersPerBox = table.Column<int>(type: "integer", nullable: false),
                    StationSlotCount = table.Column<int>(type: "integer", nullable: false),
                    BoxGreenPct = table.Column<int>(type: "integer", nullable: false),
                    BoxYellowPct = table.Column<int>(type: "integer", nullable: false),
                    LowChanceThresholdPct = table.Column<int>(type: "integer", nullable: false),
                    BulkQuantityEntry = table.Column<bool>(type: "boolean", nullable: false),
                    CargoNotifyAt = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_ful_operation_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ful_picking_plan_lines",
                schema: "fulfillment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickingPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantBarcode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Sku = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    PickedQuantity = table.Column<int>(type: "integer", nullable: false),
                    SourceBinId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceBinCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PickedBinId = table.Column<Guid>(type: "uuid", nullable: true),
                    PickedBinCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    AssignedTo = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PickedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PickedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RouteOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ful_picking_plan_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ful_picking_plan_lines_ful_picking_plans_PickingPlanId",
                        column: x => x.PickingPlanId,
                        principalSchema: "fulfillment",
                        principalTable: "ful_picking_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ful_operation_logs_OrderId_CreatedAt",
                schema: "fulfillment",
                table: "ful_operation_logs",
                columns: new[] { "OrderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ful_operation_logs_PickingPlanId_CreatedAt",
                schema: "fulfillment",
                table: "ful_operation_logs",
                columns: new[] { "PickingPlanId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ful_operation_profiles_FirmId",
                schema: "fulfillment",
                table: "ful_operation_profiles",
                column: "FirmId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ful_picking_plan_lines_OrderId",
                schema: "fulfillment",
                table: "ful_picking_plan_lines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ful_picking_plan_lines_PickingPlanId_AssignedTo",
                schema: "fulfillment",
                table: "ful_picking_plan_lines",
                columns: new[] { "PickingPlanId", "AssignedTo" });

            migrationBuilder.CreateIndex(
                name: "IX_ful_picking_plan_lines_PickingPlanId_RouteOrder",
                schema: "fulfillment",
                table: "ful_picking_plan_lines",
                columns: new[] { "PickingPlanId", "RouteOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ful_operation_logs",
                schema: "fulfillment");

            migrationBuilder.DropTable(
                name: "ful_operation_profiles",
                schema: "fulfillment");

            migrationBuilder.DropTable(
                name: "ful_picking_plan_lines",
                schema: "fulfillment");
        }
    }
}
