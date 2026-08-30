using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ECSPros.Accounts.Infrastructure.Persistence;

#nullable disable

namespace ECSPros.Accounts.Infrastructure.Migrations;

/// <summary>
/// Worker retry'larında aynı referansın aynı deftere ikinci kez yazılmasını DB düzeyinde engeller.
/// Mevcut mükerrer finansal hareketleri otomatik silmez; varsa migration bilinçli olarak durur.
/// </summary>
[DbContext(typeof(AccountsDbContext))]
[Migration("20260830203000_AddAccountTransactionIdempotency")]
public sealed class AddAccountTransactionIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_current_account_transactions_LedgerId_TransactionType_ReferenceType_ReferenceId",
            schema: "accounts",
            table: "current_account_transactions",
            columns: new[] { "LedgerId", "TransactionType", "ReferenceType", "ReferenceId" },
            unique: true,
            filter: "\"ReferenceId\" IS NOT NULL AND \"IsDeleted\" = false");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_current_account_transactions_LedgerId_TransactionType_ReferenceType_ReferenceId",
            schema: "accounts",
            table: "current_account_transactions");
    }
}
