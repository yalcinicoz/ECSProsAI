using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSortingEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "missing_card_notices",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    DescriptionText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_missing_card_notices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sorting_entries",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    LabelPrinted = table.Column<bool>(type: "boolean", nullable: false),
                    LabelCount = table.Column<int>(type: "integer", nullable: false),
                    PutawayStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PlacedBinId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlacedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StockMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    OnSaleAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_sorting_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_missing_card_notices_ReceiptBatchId",
                schema: "procurement",
                table: "missing_card_notices",
                column: "ReceiptBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_missing_card_notices_Status_CreatedAt",
                schema: "procurement",
                table: "missing_card_notices",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sorting_entries_PutawayStatus_CreatedAt",
                schema: "procurement",
                table: "sorting_entries",
                columns: new[] { "PutawayStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sorting_entries_ReceiptBatchId",
                schema: "procurement",
                table: "sorting_entries",
                column: "ReceiptBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_sorting_entries_VariantId",
                schema: "procurement",
                table: "sorting_entries",
                column: "VariantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "missing_card_notices",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sorting_entries",
                schema: "procurement");
        }
    }
}
