using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Accounts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountLedgers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "current_account_ledgers",
                schema: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_current_account_ledgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_current_account_ledgers_current_accounts_CurrentAccountId",
                        column: x => x.CurrentAccountId,
                        principalSchema: "accounts",
                        principalTable: "current_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_current_account_ledgers_CurrentAccountId_Currency",
                schema: "accounts",
                table: "current_account_ledgers",
                columns: new[] { "CurrentAccountId", "Currency" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "current_account_ledgers",
                schema: "accounts");
        }
    }
}
