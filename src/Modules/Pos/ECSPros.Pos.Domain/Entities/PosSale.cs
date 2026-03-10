using ECSPros.Pos.Domain.Events;
using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Pos.Domain.Entities;

public class PosSale : AggregateRoot
{
    public Guid SessionId { get; set; }
    public Guid RegisterId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? MemberId { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "open"; // open | completed | cancelled | refunded
    public decimal Subtotal { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalTax { get; set; }
    public decimal GrandTotal { get; set; }
    public DateTime? PrintedAt { get; set; }
    public int ReprintCount { get; set; } = 0;
    public string? Notes { get; set; }

    public PosSession Session { get; set; } = null!;
    public ICollection<PosSaleItem> Items { get; set; } = new List<PosSaleItem>();
    public ICollection<PosSalePayment> Payments { get; set; } = new List<PosSalePayment>();

    public void Complete(Guid completedBy)
    {
        Status = "completed";
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = completedBy;

        AddDomainEvent(new PosSaleCompletedEvent(Id, WarehouseId, completedBy,
            Items.Select(i => new SoldItem(i.VariantId, i.Quantity)).ToList()));
    }

    public void Refund(Guid refundedBy)
    {
        if (Status != "completed")
            throw new InvalidOperationException($"Yalnızca tamamlanmış satışlar iade edilebilir. Mevcut durum: {Status}");

        Status = "refunded";
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = refundedBy;

        AddDomainEvent(new PosSaleRefundedEvent(Id, WarehouseId, refundedBy,
            Items.Select(i => new SoldItem(i.VariantId, i.Quantity)).ToList()));
    }
}
