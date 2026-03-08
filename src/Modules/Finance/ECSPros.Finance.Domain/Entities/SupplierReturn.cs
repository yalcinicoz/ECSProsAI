using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Finance.Domain.Entities;

public class SupplierReturn : BaseEntity
{
    public Guid SupplierId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public DateOnly ReturnDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TotalTax { get; set; }
    public decimal GrandTotal { get; set; }
    public Guid? CargoFirmId { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? ShippedAt { get; set; }

    public ICollection<SupplierReturnItem> Items { get; set; } = new List<SupplierReturnItem>();
}
