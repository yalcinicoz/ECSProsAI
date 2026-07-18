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
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
