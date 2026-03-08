using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class ReturnRefund : BaseEntity
{
    public Guid ReturnId { get; set; }
    public string RefundMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object>? Details { get; set; }
    public Guid? OriginalPaymentId { get; set; }
    public Guid? WalletTransactionId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public Guid? ProcessedBy { get; set; }

    public Return Return { get; set; } = null!;
}
