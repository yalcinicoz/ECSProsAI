using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Accounts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierCommissionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "commission_group_rates",
                schema: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    RatePercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
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
                    table.PrimaryKey("PK_commission_group_rates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "settlement_lines",
                schema: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    CommissionLayer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CampaignDiscountShareAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EligibleAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AvailableAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversalOfId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_settlement_lines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_contracts",
                schema: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementDelayDays = table.Column<int>(type: "integer", nullable: false),
                    PayoutPeriod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "weekly"),
                    CargoMode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "platform_contract"),
                    TurnoverPeriodType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "monthly"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_supplier_contracts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_group_rates",
                schema: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    RatePercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
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
                    table.PrimaryKey("PK_supplier_group_rates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_group_rates_supplier_contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "accounts",
                        principalTable: "supplier_contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_product_rates",
                schema: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    RatePercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
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
                    table.PrimaryKey("PK_supplier_product_rates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_product_rates_supplier_contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "accounts",
                        principalTable: "supplier_contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_turnover_tiers",
                schema: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    MinTurnover = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RateAdjustmentPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
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
                    table.PrimaryKey("PK_supplier_turnover_tiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_turnover_tiers_supplier_contracts_ContractId",
                        column: x => x.ContractId,
                        principalSchema: "accounts",
                        principalTable: "supplier_contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_commission_group_rates_ProductGroupId",
                schema: "accounts",
                table: "commission_group_rates",
                column: "ProductGroupId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_settlement_lines_OrderItemId",
                schema: "accounts",
                table: "settlement_lines",
                column: "OrderItemId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"ReversalOfId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_settlement_lines_Status_EligibleAt",
                schema: "accounts",
                table: "settlement_lines",
                columns: new[] { "Status", "EligibleAt" });

            migrationBuilder.CreateIndex(
                name: "IX_settlement_lines_SupplierAccountId_Status",
                schema: "accounts",
                table: "settlement_lines",
                columns: new[] { "SupplierAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_contracts_CurrentAccountId",
                schema: "accounts",
                table: "supplier_contracts",
                column: "CurrentAccountId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_group_rates_ContractId_ProductGroupId",
                schema: "accounts",
                table: "supplier_group_rates",
                columns: new[] { "ContractId", "ProductGroupId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_product_rates_ContractId_ProductId",
                schema: "accounts",
                table: "supplier_product_rates",
                columns: new[] { "ContractId", "ProductId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_turnover_tiers_ContractId_MinTurnover",
                schema: "accounts",
                table: "supplier_turnover_tiers",
                columns: new[] { "ContractId", "MinTurnover" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commission_group_rates",
                schema: "accounts");

            migrationBuilder.DropTable(
                name: "settlement_lines",
                schema: "accounts");

            migrationBuilder.DropTable(
                name: "supplier_group_rates",
                schema: "accounts");

            migrationBuilder.DropTable(
                name: "supplier_product_rates",
                schema: "accounts");

            migrationBuilder.DropTable(
                name: "supplier_turnover_tiers",
                schema: "accounts");

            migrationBuilder.DropTable(
                name: "supplier_contracts",
                schema: "accounts");
        }
    }
}
