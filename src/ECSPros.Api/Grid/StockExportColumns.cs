namespace ECSPros.Api.Grid;

/// <summary>Admin stok listesi Excel kolonları (anahtarlar panel DataGrid kolon anahtarlarıyla aynı; ürün kodu + depo kilitli).</summary>
public static class StockExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<StockExportRow>> All = new GridExportColumn<StockExportRow>[]
    {
        new("productCode", "Ürün Kodu", r => r.ProductCode, Locked: true),
        new("productName", "Ürün Adı", r => r.ProductName),
        new("options", "Seçenekler", r => r.Options),
        new("barcode", "Barkod", r => r.Barcode),
        new("warehouse", "Depo", r => r.WarehouseName, Locked: true),
        new("section", "Kısım", r => r.SectionName),
        new("bin", "Raf", r => r.BinCode),
        new("quantity", "Stok", r => r.Quantity),
        new("reserved", "Rezerve", r => r.ReservedQuantity),
        new("available", "Mevcut", r => r.AvailableQuantity),
        new("stockType", "Stok Tipi", r => r.StockType == "virtual" ? "Sanal" : "Fiziksel"),
        new("updatedAt", "Güncellenme", r => r.UpdatedAt.HasValue ? GridExportWriter.ToIstanbul(r.UpdatedAt.Value) : null),
    };
}
