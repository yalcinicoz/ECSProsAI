using ECSPros.Catalog.Application.Queries.GetProducts;

namespace ECSPros.Api.Grid;

/// <summary>Ürünler Excel kolonları (anahtarlar panel DataGrid kolon anahtarlarıyla aynı; ürün kodu + ad kilitli).</summary>
public static class ProductExportColumns
{
    private static string Tr(Dictionary<string, string>? d) => d is null ? "" : d.TryGetValue("tr", out var v) ? v : d.Values.FirstOrDefault() ?? "";

    public static readonly IReadOnlyList<GridExportColumn<ProductExportRow>> All = new GridExportColumn<ProductExportRow>[]
    {
        new("code", "Ürün Kodu", r => r.Code, Locked: true),
        new("name", "Ürün Adı", r => Tr(r.NameI18n), Locked: true),
        new("group", "Grup", r => Tr(r.GroupNameI18n)),
        new("groupCode", "Grup Kodu", r => r.GroupCode),
        new("isSaleOpen", "Satış Durumu", r => r.IsSaleOpen ? "Satışta" : "Satış Kapalı"),
        new("basePrice", "Liste Fiyatı", r => r.BasePrice),
        new("baseCost", "Maliyet", r => r.BaseCost, AlanYetkisi: "cost"),
        new("taxRate", "KDV %", r => r.TaxRate),
        new("sourceType", "Kaynak", r => ProductGrid.SourceTypeLabel(r.SourceType)),
        new("supplierProductCode", "Tedarikçi Ürün Kodu", r => r.SupplierProductCode),
        new("variantCount", "Varyant Sayısı", r => r.VariantCount),
        new("imageState", "Görsel Durumu", r => ProductGrid.ImageStateLabel(r.ImageState)),
        new("videoState", "Video Durumu", r => r.HasVideo ? "Var" : "Yok"),
        new("stock", "Stok (fiziksel)", r => r.StockQuantity),
        new("stockAvailable", "Satılabilir Stok", r => r.StockAvailable),
        new("slug", "Slug", r => r.Slug),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
