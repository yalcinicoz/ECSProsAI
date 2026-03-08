using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Finance.Domain.Entities;

public class SupplierDeliveryItem : BaseEntity
{
    public Guid DeliveryId { get; set; }
    public Guid? InvoiceItemId { get; set; }
    public Guid VariantId { get; set; }
    public int ExpectedQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public int RejectedQuantity { get; set; } = 0;
    public Guid? LocationId { get; set; }
    public string? Notes { get; set; }

    public SupplierDelivery Delivery { get; set; } = null!;
}
