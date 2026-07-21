using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Inventory.Domain.Entities;

/// <summary>
/// Üçlü depo yapısında orta katman: Depo (fiziki) → <b>Kısım</b> (kat/ana bölme) → Birim/Raf.
/// İnternet satışına açma/kapama KISIM seviyesinde yönetilir (<see cref="IsSellableOnline"/>) —
/// depo tümden kapatılamaz. Eski sistemdeki <c>dfstorages</c> karşılığı; mağazalarda
/// şimdilik tek kısım yeterli. (Onaylanan tasarım 2026-07-14.)
/// </summary>
public class WarehouseSection : BaseEntity
{
    public Guid WarehouseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Dolu ise bu kısım bir tedarikçinin (accounts.current_accounts.Id) stok alanıdır —
    /// Partner API `PUT /stock` buraya yazar; owner-scope bu alanla (§3.7 / F2b-2b). Null → normal kısım.</summary>
    public Guid? SupplierId { get; set; }

    /// <summary>Bu kısımdaki serbest stok siteye "stokta var" sayılır mı — yönetim noktası.</summary>
    public bool IsSellableOnline { get; set; } = true;

    public int PickingOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public Warehouse Warehouse { get; set; } = null!;
    public ICollection<WarehouseBin> Bins { get; set; } = new List<WarehouseBin>();
}
