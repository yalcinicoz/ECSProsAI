using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Fulfillment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSortingBoxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SortingBoxId",
                schema: "fulfillment",
                table: "ful_sorting_bins",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortedQuantity",
                schema: "fulfillment",
                table: "ful_picking_plan_lines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ful_sorting_boxes",
                schema: "fulfillment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PickingPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoxNumber = table.Column<int>(type: "integer", nullable: false),
                    Generation = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TakenBy = table.Column<Guid>(type: "uuid", nullable: true),
                    TakenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StationId = table.Column<Guid>(type: "uuid", nullable: true),
                    StationNumber = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_ful_sorting_boxes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ful_sorting_boxes_PickingPlanId_BoxNumber_Generation",
                schema: "fulfillment",
                table: "ful_sorting_boxes",
                columns: new[] { "PickingPlanId", "BoxNumber", "Generation" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ful_sorting_boxes",
                schema: "fulfillment");

            migrationBuilder.DropColumn(
                name: "SortingBoxId",
                schema: "fulfillment",
                table: "ful_sorting_bins");

            migrationBuilder.DropColumn(
                name: "SortedQuantity",
                schema: "fulfillment",
                table: "ful_picking_plan_lines");
        }
    }
}
