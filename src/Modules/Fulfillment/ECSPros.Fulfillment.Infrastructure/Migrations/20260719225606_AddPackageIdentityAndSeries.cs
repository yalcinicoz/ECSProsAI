using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Fulfillment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageIdentityAndSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PackageNumber",
                schema: "fulfillment",
                table: "ful_packages",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "CargoIntegrationCode",
                schema: "fulfillment",
                table: "ful_packages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CargoIntegrationCodeSource",
                schema: "fulfillment",
                table: "ful_packages",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FirmPlatformId",
                schema: "fulfillment",
                table: "ful_packages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "SequenceInOrder",
                schema: "fulfillment",
                table: "ful_packages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                schema: "fulfillment",
                table: "ful_packages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ful_package_code_history",
                schema: "fulfillment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldPackageNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    OldCargoIntegrationCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ChangeType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_ful_package_code_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ful_package_items",
                schema: "fulfillment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ful_package_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ful_package_items_ful_packages_PackageId",
                        column: x => x.PackageId,
                        principalSchema: "fulfillment",
                        principalTable: "ful_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ful_package_number_series",
                schema: "fulfillment",
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
                    table.PrimaryKey("PK_ful_package_number_series", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ful_packages_FirmPlatformId_PackageNumber",
                schema: "fulfillment",
                table: "ful_packages",
                columns: new[] { "FirmPlatformId", "PackageNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ful_packages_OrderId",
                schema: "fulfillment",
                table: "ful_packages",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ful_package_code_history_PackageId",
                schema: "fulfillment",
                table: "ful_package_code_history",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ful_package_items_OrderItemId",
                schema: "fulfillment",
                table: "ful_package_items",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ful_package_items_PackageId",
                schema: "fulfillment",
                table: "ful_package_items",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ful_package_number_series_FirmPlatformId",
                schema: "fulfillment",
                table: "ful_package_number_series",
                column: "FirmPlatformId",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ful_package_code_history",
                schema: "fulfillment");

            migrationBuilder.DropTable(
                name: "ful_package_items",
                schema: "fulfillment");

            migrationBuilder.DropTable(
                name: "ful_package_number_series",
                schema: "fulfillment");

            migrationBuilder.DropIndex(
                name: "IX_ful_packages_FirmPlatformId_PackageNumber",
                schema: "fulfillment",
                table: "ful_packages");

            migrationBuilder.DropIndex(
                name: "IX_ful_packages_OrderId",
                schema: "fulfillment",
                table: "ful_packages");

            migrationBuilder.DropColumn(
                name: "CargoIntegrationCode",
                schema: "fulfillment",
                table: "ful_packages");

            migrationBuilder.DropColumn(
                name: "CargoIntegrationCodeSource",
                schema: "fulfillment",
                table: "ful_packages");

            migrationBuilder.DropColumn(
                name: "FirmPlatformId",
                schema: "fulfillment",
                table: "ful_packages");

            migrationBuilder.DropColumn(
                name: "SequenceInOrder",
                schema: "fulfillment",
                table: "ful_packages");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                schema: "fulfillment",
                table: "ful_packages");

            migrationBuilder.AlterColumn<int>(
                name: "PackageNumber",
                schema: "fulfillment",
                table: "ful_packages",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);
        }
    }
}
