using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Infrastructure.Persistence;

public class CrmDbContext : DbContext, ICrmDbContext
{
    public CrmDbContext(DbContextOptions<CrmDbContext> options) : base(options) { }

    public DbSet<MemberGroup> MemberGroups => Set<MemberGroup>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Neighborhood> Neighborhoods => Set<Neighborhood>();
    public DbSet<Street> Streets => Set<Street>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<MemberCredit> MemberCredits => Set<MemberCredit>();
    public DbSet<MemberPrice> MemberPrices => Set<MemberPrice>();
    public DbSet<MemberDiscount> MemberDiscounts => Set<MemberDiscount>();
    public DbSet<OrderTemplate> OrderTemplates => Set<OrderTemplate>();
    public DbSet<OrderTemplateItem> OrderTemplateItems => Set<OrderTemplateItem>();
    public DbSet<TicketStatus> TicketStatuses => Set<TicketStatus>();
    public DbSet<TicketSubject> TicketSubjects => Set<TicketSubject>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketActivity> TicketActivities => Set<TicketActivity>();
    public DbSet<TicketRead> TicketReads => Set<TicketRead>();
    public DbSet<TicketNotification> TicketNotifications => Set<TicketNotification>();
    public DbSet<TicketLegacyStaff> TicketLegacyStaff => Set<TicketLegacyStaff>();

    public async Task<long> NextTicketSequenceAsync(CancellationToken ct = default)
        => await Database.SqlQueryRaw<long>("SELECT nextval('crm.crm_ticket_tracking_seq') AS \"Value\"").SingleAsync(ct);
    public DbSet<MemberSession> MemberSessions => Set<MemberSession>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<MemberExternalLogin> MemberExternalLogins => Set<MemberExternalLogin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("crm");
        modelBuilder.HasSequence<long>("crm_ticket_tracking_seq", "crm").StartsAt(50000).IncrementsBy(1);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
