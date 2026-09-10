using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBinCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inv_bin_counts",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    BinId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpectedTotal = table.Column<int>(type: "integer", nullable: false),
                    CountedTotal = table.Column<int>(type: "integer", nullable: false),
                    DiffTotal = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_inv_bin_counts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inv_bin_counts_inv_warehouse_bins_BinId",
                        column: x => x.BinId,
                        principalSchema: "inventory",
                        principalTable: "inv_warehouse_bins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inv_bin_counts_inv_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "inventory",
                        principalTable: "inv_warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inv_bin_count_lines",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BinCountId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedQuantity = table.Column<int>(type: "integer", nullable: false),
                    CountedQuantity = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_inv_bin_count_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inv_bin_count_lines_inv_bin_counts_BinCountId",
                        column: x => x.BinCountId,
                        principalSchema: "inventory",
                        principalTable: "inv_bin_counts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inv_bin_count_lines_BinCountId_VariantId",
                schema: "inventory",
                table: "inv_bin_count_lines",
                columns: new[] { "BinCountId", "VariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inv_bin_counts_BinId_Status",
                schema: "inventory",
                table: "inv_bin_counts",
                columns: new[] { "BinId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_inv_bin_counts_StartedAt",
                schema: "inventory",
                table: "inv_bin_counts",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_inv_bin_counts_WarehouseId",
                schema: "inventory",
                table: "inv_bin_counts",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inv_bin_count_lines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inv_bin_counts",
                schema: "inventory");
        }
    }
}
