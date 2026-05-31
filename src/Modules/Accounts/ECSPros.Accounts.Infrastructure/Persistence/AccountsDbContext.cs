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
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("accounts");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountsDbContext).Assembly);
        modelBuilder.Entity<CurrentAccountGroup>().HasQueryFilter(g => !g.IsDeleted);
        modelBuilder.Entity<CurrentAccount>().HasQueryFilter(a => !a.IsDeleted);
        modelBuilder.Entity<CurrentAccountLedger>().HasQueryFilter(l => !l.IsDeleted);
    }
}
