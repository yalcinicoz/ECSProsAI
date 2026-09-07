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
    DbSet<Neighborhood> Neighborhoods { get; }
    DbSet<MemberSession> MemberSessions { get; }
    DbSet<OtpCode> OtpCodes { get; }
    DbSet<MemberExternalLogin> MemberExternalLogins { get; }

    // Müşteri İlişkileri (talep/şikayet kayıtları, 2026-09-07)
    DbSet<TicketStatus> TicketStatuses { get; }
    DbSet<TicketSubject> TicketSubjects { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<TicketActivity> TicketActivities { get; }
    DbSet<TicketRead> TicketReads { get; }
    DbSet<TicketNotification> TicketNotifications { get; }
    DbSet<TicketLegacyStaff> TicketLegacyStaff { get; }
    /// <summary>Takip no sırası (crm.crm_ticket_tracking_seq): takip no = unix saniye + sıra (K4).</summary>
    Task<long> NextTicketSequenceAsync(CancellationToken ct = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
