using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Finance.Domain.Entities;

public class SupplierDelivery : BaseEntity
{
    public Guid SupplierId { get; set; }
    public Guid? InvoiceId { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public string? DeliveryNoteNumber { get; set; }
    public Guid WarehouseId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid? ReceivedBy { get; set; }
    public DateTime? ReceivedAt { get; set; }

    public ICollection<SupplierDeliveryItem> Items { get; set; } = new List<SupplierDeliveryItem>();
}
