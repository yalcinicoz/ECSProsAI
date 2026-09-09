using ECSPros.Procurement.Application.Queries.GetPurchaseOrders;

namespace ECSPros.Api.Grid;

/// <summary>Satın alma siparişleri Excel kolonları (kod kilitli).</summary>
public static class PurchaseOrderExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<PurchaseOrderExportRow>> All = new GridExportColumn<PurchaseOrderExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("orderDate", "Sipariş Tarihi", r => GridExportWriter.ToIstanbul(r.OrderDate)),
        new("expectedDate", "Beklenen", r => r.ExpectedDate.HasValue ? GridExportWriter.ToIstanbul(r.ExpectedDate.Value) : null),
        new("status", "Durum", r => PurchaseOrderGrid.StatusLabel(r.Status)),
        new("itemCount", "Kalem", r => r.ItemCount),
        new("totalQuantity", "Toplam Adet", r => r.TotalQuantity),
        new("totalAmount", "Toplam Tutar", r => r.TotalAmount),
        new("notes", "Not", r => r.Notes),
    };
}
