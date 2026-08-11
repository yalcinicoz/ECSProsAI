using ECSPros.Accounts.Application.Services;
using ECSPros.Accounts.Domain.Entities;
using ECSPros.Accounts.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
namespace ECSPros.Accounts.Infrastructure.Persistence;
public class AccountsDbContext : DbContext, IAccountsDbContext
{
    public AccountsDbContext(DbContextOptions<AccountsDbContext> options) : base(options) { }
    public DbSet<CurrentAccountGroup> AccountGroups => Set<CurrentAccountGroup>();
    public DbSet<CurrentAccount> CurrentAccounts => Set<CurrentAccount>();
    public DbSet<CurrentAccountLedger> AccountLedgers => Set<CurrentAccountLedger>();
    public DbSet<CurrentAccountTransaction> AccountTransactions => Set<CurrentAccountTransaction>();
    public DbSet<SupplierContract> SupplierContracts => Set<SupplierContract>();
    public DbSet<SupplierGroupRate> SupplierGroupRates => Set<SupplierGroupRate>();
    public DbSet<SupplierProductRate> SupplierProductRates => Set<SupplierProductRate>();
    public DbSet<SupplierTurnoverTier> SupplierTurnoverTiers => Set<SupplierTurnoverTier>();
    public DbSet<CommissionGroupRate> CommissionGroupRates => Set<CommissionGroupRate>();
    public DbSet<SettlementLine> SettlementLines => Set<SettlementLine>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("accounts");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountsDbContext).Assembly);
        modelBuilder.Entity<CurrentAccountGroup>().HasQueryFilter(g => !g.IsDeleted);
        modelBuilder.Entity<CurrentAccount>().HasQueryFilter(a => !a.IsDeleted);
        modelBuilder.Entity<CurrentAccountLedger>().HasQueryFilter(l => !l.IsDeleted);
        modelBuilder.Entity<CurrentAccountTransaction>().HasQueryFilter(t => !t.IsDeleted);
        modelBuilder.Entity<SupplierContract>().HasQueryFilter(c => !c.IsDeleted);
        modelBuilder.Entity<SupplierGroupRate>().HasQueryFilter(r => !r.IsDeleted);
        modelBuilder.Entity<SupplierProductRate>().HasQueryFilter(r => !r.IsDeleted);
        modelBuilder.Entity<SupplierTurnoverTier>().HasQueryFilter(t => !t.IsDeleted);
        modelBuilder.Entity<CommissionGroupRate>().HasQueryFilter(r => !r.IsDeleted);
        modelBuilder.Entity<SettlementLine>().HasQueryFilter(l => !l.IsDeleted);
    }
}
