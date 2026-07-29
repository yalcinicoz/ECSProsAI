using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

/// <summary>Paket içeriği — sipariş kaleminin (order.ord_order_items) pakete atanan
/// kısmı. Tedarikçi bazlı bölme ve paket başına fatura bu atamayla izlenir (F2).</summary>
public class PackageItem : BaseEntity
{
    public Guid PackageId { get; set; }
    public Guid OrderItemId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }

    public Package Package { get; set; } = null!;
}
