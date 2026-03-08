using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class GiftCard : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Guid FirmId { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public bool IsSingleUse { get; set; } = false;
    public Guid? CreatedForMemberId { get; set; }
    public Guid? CreatedFromOrderId { get; set; }
    public string Status { get; set; } = string.Empty;

    public ICollection<GiftCardTransaction> Transactions { get; set; } = new List<GiftCardTransaction>();
}
