using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Inventory.Domain.Entities;

public class Warehouse : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string WarehouseType { get; set; } = "main"; // main, secondary, store, store_warehouse, virtual, receiving, studio, tailor, defective, other
    public string? Address { get; set; }
    /// <summary>Depo düzeyi üst kapı (2026-09-11'den beri ETKİN): kapalıysa deponun hiçbir kısmındaki stok
    /// sitede/kanalda sayılmaz ve rezervasyona girmez; açıksa karar kısımlara (<see cref="WarehouseSection.IsSellableOnline"/>) kalır.</summary>
    public bool IsSellableOnline { get; set; } = true;
    public int ReservePriority { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    // Üçlü depo yapısı (onaylanan tasarım 2026-07-14). Cutover'a dek mevcut alanlarla
    // bir arada durur; satışa-açıklık kısımda, depo bayrağı üst kapı olarak kaldı (2026-09-11).
    /// <summary>Sipariş konsolidasyonunun yapıldığı tek merkez depo işareti.</summary>
    public bool IsCentral { get; set; } = false;
    /// <summary>ERP'nin gördüğü depo kodu (ör. D012) — birden çok fiziki depo tek ERP koduna eşlenebilir.</summary>
    public string? ErpCode { get; set; }

    public ICollection<WarehouseLocation> Locations { get; set; } = new List<WarehouseLocation>();
    public ICollection<WarehouseSection> Sections { get; set; } = new List<WarehouseSection>();
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
}
