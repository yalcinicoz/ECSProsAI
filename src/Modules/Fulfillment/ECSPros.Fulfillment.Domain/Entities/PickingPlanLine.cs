using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

/// <summary>
/// OP1 (2026-08-09): toplama görevi satırı — sipariş kalemi × önerilen kaynak raf ×
/// atanan personel. Personel dağıtımı ve toplama ilerlemesi satır üzerinden izlenir;
/// OrderItem.PickAssignedTo/PickedBy alanları event'le senkron tutulur.
/// K-15: fiilen toplanan raf (PickedBin*) ayrıca saklanır — önerilenden farklı olabilir.
/// </summary>
public class PickingPlanLine : BaseEntity
{
    public Guid PickingPlanId { get; set; }
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public Guid VariantId { get; set; }

    /// <summary>Okutma eşleşme anahtarı (legacy dersi: eşleşme VARYANT BARKODU iledir).</summary>
    public string VariantBarcode { get; set; } = string.Empty;

    // Ekranlar için denormalize — modüller arası join gerektirmez
    public string OrderNumber { get; set; } = string.Empty;
    /// <summary>OP2: "en eski sipariş önce" kuralı (K-7/K-12) — sipariş tarihi denormalize.</summary>
    public DateTime OrderCreatedAt { get; set; }
    public string DisplayName { get; set; } = string.Empty; // ProductName + VariantInfo
    public string Sku { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public int PickedQuantity { get; set; }

    /// <summary>Önerilen kaynak raf (rezervasyondan; rota = Section.PickingOrder, Bin.PickingOrder).</summary>
    public Guid? SourceBinId { get; set; }
    public string? SourceBinCode { get; set; }

    /// <summary>K-15: fiilen toplanan raf (varsayılan önerilen; personel raf barkodu okutarak değiştirir).</summary>
    public Guid? PickedBinId { get; set; }
    public string? PickedBinCode { get; set; }

    public Guid? AssignedTo { get; set; }
    public DateTime? AssignedAt { get; set; }
    public Guid? PickedBy { get; set; }
    public DateTime? PickedAt { get; set; }

    /// <summary>pending | assigned | picked | short | returned</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Rota sırası — plan üretiminde kısım/raf PickingOrder'ına göre yazılır.</summary>
    public int RouteOrder { get; set; }

    public PickingPlan? PickingPlan { get; set; }
}
