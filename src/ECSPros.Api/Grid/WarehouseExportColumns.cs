using ECSPros.Inventory.Application.Queries.GetWarehouses;

namespace ECSPros.Api.Grid;

/// <summary>Depolar Excel kolonları (kod kilitli).</summary>
public static class WarehouseExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<WarehouseExportRow>> All = new GridExportColumn<WarehouseExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("name", "Ad", r => r.Name),
        new("warehouseType", "Tip", r => r.WarehouseType),
        new("isSellableOnline", "Online Satış", r => r.IsSellableOnline ? "Evet" : "Hayır"),
        new("isActive", "Aktif", r => r.IsActive ? "Evet" : "Hayır"),
        new("address", "Adres", r => r.Address, AlanYetkisi: "address"),
        new("sectionCount", "Kısım", r => r.SectionCount),
        new("sortOrder", "Sıra", r => r.SortOrder),
        new("reservePriority", "Rezerv Önceliği", r => r.ReservePriority),
        new("isCentral", "Merkez", r => r.IsCentral ? "Evet" : "Hayır"),
        new("erpCode", "ERP Kodu", r => r.ErpCode),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
