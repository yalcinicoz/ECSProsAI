using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class WalletTransaction : BaseEntity
{
    public Guid WalletId { get; set; }
    public string TransactionType { get; set; } = string.Empty; // refund_credit, payment_usage, manual_adjustment, withdrawal
    public decimal Debit { get; set; } = 0;
    public decimal Credit { get; set; } = 0;
    public decimal BalanceAfter { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Description { get; set; }

    public Wallet Wallet { get; set; } = null!;
}
