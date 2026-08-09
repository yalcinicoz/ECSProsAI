using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Fulfillment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPackingDesks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeskSlotNumber",
                schema: "fulfillment",
                table: "ful_sorting_bins",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ObmTransferred",
                schema: "fulfillment",
                table: "ful_sorting_bins",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FinalScannedQuantity",
                schema: "fulfillment",
                table: "ful_picking_plan_lines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FinalSortedQuantity",
                schema: "fulfillment",
                table: "ful_picking_plan_lines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ful_packing_desks",
                schema: "fulfillment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickingPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortingBoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeskNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OpenedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_ful_packing_desks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ful_packing_desks_PickingPlanId_DeskNumber_Status",
                schema: "fulfillment",
                table: "ful_packing_desks",
                columns: new[] { "PickingPlanId", "DeskNumber", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ful_packing_desks_SortingBoxId",
                schema: "fulfillment",
                table: "ful_packing_desks",
                column: "SortingBoxId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ful_packing_desks",
                schema: "fulfillment");

            migrationBuilder.DropColumn(
                name: "DeskSlotNumber",
                schema: "fulfillment",
                table: "ful_sorting_bins");

            migrationBuilder.DropColumn(
                name: "ObmTransferred",
                schema: "fulfillment",
                table: "ful_sorting_bins");

            migrationBuilder.DropColumn(
                name: "FinalScannedQuantity",
                schema: "fulfillment",
                table: "ful_picking_plan_lines");

            migrationBuilder.DropColumn(
                name: "FinalSortedQuantity",
                schema: "fulfillment",
                table: "ful_picking_plan_lines");
        }
    }
}
