using ECSPros.Accounts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
namespace ECSPros.Accounts.Application.Services;
public interface IAccountsDbContext
{
    DbSet<CurrentAccountGroup> AccountGroups { get; }
    DbSet<CurrentAccount> CurrentAccounts { get; }
    DbSet<CurrentAccountLedger> AccountLedgers { get; }
    DbSet<CurrentAccountTransaction> AccountTransactions { get; }
    DbSet<SupplierContract> SupplierContracts { get; }
    DbSet<SupplierGroupRate> SupplierGroupRates { get; }
    DbSet<SupplierProductRate> SupplierProductRates { get; }
    DbSet<SupplierTurnoverTier> SupplierTurnoverTiers { get; }
    DbSet<CommissionGroupRate> CommissionGroupRates { get; }
    DbSet<SettlementLine> SettlementLines { get; }
    DatabaseFacade Database { get; }
    Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker ChangeTracker { get; }   // Faz 1: retry sarması gövde temizliği
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
