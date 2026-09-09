using ECSPros.Storefront.Application.Queries.GetSavedSearchesForAdmin;
using ECSPros.Storefront.Application.Queries.GetStockAlertsForAdmin;

namespace ECSPros.Api.Grid;

/// <summary>Stok alarmları Excel kolonları — e-posta kişisel veri, alan yetkisine bağlı.</summary>
public static class StockAlertExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<StockAlertExportRow>> All = new GridExportColumn<StockAlertExportRow>[]
    {
        new("productCode", "Ürün Kodu", r => r.ProductCode, Locked: true),
        new("variantInfo", "Varyant", r => r.VariantInfo),
        new("email", "E-posta", r => r.Email, AlanYetkisi: "email"),
        new("status", "Durum", r => StockAlertGrid.StatusLabel(r.Status)),
        new("createdAt", "Kayıt", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
        new("notifiedAt", "Bildirim", r => r.NotifiedAt.HasValue ? GridExportWriter.ToIstanbul(r.NotifiedAt.Value) : null),
    };
}

/// <summary>Kayıtlı aramalar Excel kolonları.</summary>
public static class SavedSearchExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<SavedSearchExportRow>> All = new GridExportColumn<SavedSearchExportRow>[]
    {
        new("name", "Ad", r => r.Name, Locked: true),
        new("query", "Sorgu", r => r.Query),
        new("notifyEnabled", "Bildirim", r => r.NotifyEnabled ? "Açık" : "Kapalı"),
        new("createdAt", "Kayıt", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
        new("lastNotifiedAt", "Son Bildirim", r => r.LastNotifiedAt.HasValue ? GridExportWriter.ToIstanbul(r.LastNotifiedAt.Value) : null),
    };
}
