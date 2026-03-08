using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class OrderGift : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; } = 1;
    public string? GiftReason { get; set; }
    public Guid? CampaignId { get; set; }
    public string AddedAtStage { get; set; } = string.Empty;
    public Guid? AddedBy { get; set; }
    public bool ShowOnInvoice { get; set; } = true;
    public string? InvoiceDescription { get; set; }
    public decimal UnitValue { get; set; } = 0;

    public Order Order { get; set; } = null!;
}
