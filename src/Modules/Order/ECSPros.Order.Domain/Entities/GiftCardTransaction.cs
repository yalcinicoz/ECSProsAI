using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class GiftCardTransaction : BaseEntity
{
    public Guid GiftCardId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public Guid? OrderId { get; set; }
    public string? Notes { get; set; }

    public GiftCard GiftCard { get; set; } = null!;
}
