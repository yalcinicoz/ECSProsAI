using ECSPros.Crm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Services;

public interface ICrmDbContext
{
    DbSet<MemberGroup> MemberGroups { get; }
    DbSet<Member> Members { get; }
    DbSet<Address> Addresses { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Wallet> Wallets { get; }
    DbSet<WalletTransaction> WalletTransactions { get; }
    DbSet<LoyaltyAccount> LoyaltyAccounts { get; }
    DbSet<LoyaltyTransaction> LoyaltyTransactions { get; }
    DbSet<Country> Countries { get; }
    DbSet<City> Cities { get; }
    DbSet<District> Districts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
