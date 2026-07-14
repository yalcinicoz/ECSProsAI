using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Inventory.Domain.Entities;

/// <summary>
/// Üçlü depo yapısının en alt katmanı: Depo → Kısım → <b>Birim/Raf</b>. Barkodla okutulan
/// fiziksel gözdür; eski sistemdeki <c>dfstorageunits</c> karşılığı (mevcut
/// <see cref="WarehouseLocation"/>'ın sadeleşmiş hali — ParentId/LocationType hiyerarşisi yok).
/// Reyon depolarında raf takibi yoksa tek "dummy" birim yeterli. (Onaylanan tasarım 2026-07-14.)
/// </summary>
public class WarehouseBin : BaseEntity
{
    public Guid SectionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int PickingOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public WarehouseSection Section { get; set; } = null!;
}
