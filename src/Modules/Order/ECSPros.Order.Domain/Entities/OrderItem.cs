using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid VariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string VariantInfo { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;

    // Picking
    public Guid? PickAssignedTo { get; set; }
    public DateTime? PickAssignedAt { get; set; }
    public Guid? PickedBy { get; set; }
    public DateTime? PickedAt { get; set; }

    // Sorting
    public int SortingBinQuantity { get; set; } = 0;
    public int FinalSortQuantity { get; set; } = 0;

    // Final Scan
    public Guid? FinalScanBy { get; set; }
    public DateTime? FinalScanAt { get; set; }
    public int FinalScanQuantity { get; set; } = 0;

    public Order Order { get; set; } = null!;
}
