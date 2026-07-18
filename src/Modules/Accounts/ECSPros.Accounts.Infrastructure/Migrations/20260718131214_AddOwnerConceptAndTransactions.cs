using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Accounts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerConceptAndTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_current_account_ledgers_CurrentAccountId_Currency",
                schema: "accounts",
                table: "current_account_ledgers");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                schema: "accounts",
                table: "current_accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerType",
                schema: "accounts",
                table: "current_accounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "external");

            migrationBuilder.AddColumn<string>(
                name: "ConceptCode",
                schema: "accounts",
                table: "current_account_ledgers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "cari");

            migrationBuilder.CreateTable(
                name: "current_account_transactions",
                schema: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Debit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("PK_current_account_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_current_account_transactions_current_account_ledgers_Ledger~",
                        column: x => x.LedgerId,
                        principalSchema: "accounts",
                        principalTable: "current_account_ledgers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_current_accounts_OwnerType_OwnerId",
                schema: "accounts",
                table: "current_accounts",
                columns: new[] { "OwnerType", "OwnerId" },
                unique: true,
                filter: "\"OwnerId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_current_account_ledgers_CurrentAccountId_ConceptCode_Curren~",
                schema: "accounts",
                table: "current_account_ledgers",
                columns: new[] { "CurrentAccountId", "ConceptCode", "Currency" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_current_account_transactions_LedgerId_CreatedAt",
                schema: "accounts",
                table: "current_account_transactions",
                columns: new[] { "LedgerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_current_account_transactions_ReferenceType_ReferenceId",
                schema: "accounts",
                table: "current_account_transactions",
                columns: new[] { "ReferenceType", "ReferenceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "current_account_transactions",
                schema: "accounts");

            migrationBuilder.DropIndex(
                name: "IX_current_accounts_OwnerType_OwnerId",
                schema: "accounts",
                table: "current_accounts");

            migrationBuilder.DropIndex(
                name: "IX_current_account_ledgers_CurrentAccountId_ConceptCode_Curren~",
                schema: "accounts",
                table: "current_account_ledgers");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                schema: "accounts",
                table: "current_accounts");

            migrationBuilder.DropColumn(
                name: "OwnerType",
                schema: "accounts",
                table: "current_accounts");

            migrationBuilder.DropColumn(
                name: "ConceptCode",
                schema: "accounts",
                table: "current_account_ledgers");

            migrationBuilder.CreateIndex(
                name: "IX_current_account_ledgers_CurrentAccountId_Currency",
                schema: "accounts",
                table: "current_account_ledgers",
                columns: new[] { "CurrentAccountId", "Currency" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }
    }
}
