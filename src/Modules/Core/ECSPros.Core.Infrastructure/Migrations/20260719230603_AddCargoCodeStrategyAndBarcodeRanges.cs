using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCargoCodeStrategyAndBarcodeRanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CargoCodeCharset",
                schema: "definition",
                table: "integration_services",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CargoCodeMaxLength",
                schema: "definition",
                table: "integration_services",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CargoCodeMinLength",
                schema: "definition",
                table: "integration_services",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CargoCodeStrategy",
                schema: "definition",
                table: "integration_services",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "core_cargo_barcode_ranges",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformIntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RangeStart = table.Column<long>(type: "bigint", nullable: false),
                    RangeEnd = table.Column<long>(type: "bigint", nullable: false),
                    NextValue = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExhaustedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_core_cargo_barcode_ranges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_cargo_barcode_ranges_core_firm_platform_integrations_F~",
                        column: x => x.FirmPlatformIntegrationId,
                        principalSchema: "core",
                        principalTable: "core_firm_platform_integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_cargo_barcode_ranges_FirmPlatformIntegrationId",
                schema: "core",
                table: "core_cargo_barcode_ranges",
                column: "FirmPlatformIntegrationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "core_cargo_barcode_ranges",
                schema: "core");

            migrationBuilder.DropColumn(
                name: "CargoCodeCharset",
                schema: "definition",
                table: "integration_services");

            migrationBuilder.DropColumn(
                name: "CargoCodeMaxLength",
                schema: "definition",
                table: "integration_services");

            migrationBuilder.DropColumn(
                name: "CargoCodeMinLength",
                schema: "definition",
                table: "integration_services");

            migrationBuilder.DropColumn(
                name: "CargoCodeStrategy",
                schema: "definition",
                table: "integration_services");
        }
    }
}
