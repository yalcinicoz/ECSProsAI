using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class LoyaltyTransaction : BaseEntity
{
    public Guid LoyaltyAccountId { get; set; }
    public string TransactionType { get; set; } = string.Empty; // earn, redeem, expire, cancel, adjustment
    public int Points { get; set; }
    public int BalanceAfter { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Notes { get; set; }

    public LoyaltyAccount LoyaltyAccount { get; set; } = null!;
}
