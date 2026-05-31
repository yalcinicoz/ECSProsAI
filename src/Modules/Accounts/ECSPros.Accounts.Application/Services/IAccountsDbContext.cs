using ECSPros.Accounts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace ECSPros.Accounts.Application.Services;
public interface IAccountsDbContext
{
    DbSet<CurrentAccountGroup> AccountGroups { get; }
    DbSet<CurrentAccount> CurrentAccounts { get; }
    DbSet<CurrentAccountLedger> AccountLedgers { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
