using ECSPros.Pos.Application.Queries.GetPosSales;

namespace ECSPros.Api.Grid;

/// <summary>POS satışları Excel kolonları (fiş no kilitli).</summary>
public static class PosSaleExportColumns
{
    private static object? Tarih(DateTime? d) => d.HasValue ? GridExportWriter.ToIstanbul(d.Value) : null;

    public static readonly IReadOnlyList<GridExportColumn<PosSaleExportRow>> All = new GridExportColumn<PosSaleExportRow>[]
    {
        new("saleNumber", "Fiş No", r => r.SaleNumber, Locked: true),
        new("status", "Durum", r => PosSaleGrid.StatusLabel(r.Status)),
        new("register", "Kasa", r => r.RegisterName),
        new("session", "Oturum", r => r.SessionNumber),
        new("createdAt", "Tarih", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
        new("itemCount", "Kalem", r => r.ItemCount),
        new("subtotal", "Ara Toplam", r => r.Subtotal),
        new("totalDiscount", "İndirim", r => r.TotalDiscount),
        new("totalTax", "KDV", r => r.TotalTax),
        new("total", "Genel Toplam", r => r.GrandTotal),
        new("printedAt", "Fiş Basımı", r => Tarih(r.PrintedAt)),
        new("reprintCount", "Yeniden Basım", r => r.ReprintCount),
        new("memberId", "Üye Id", r => r.MemberId?.ToString()),
        new("notes", "Not", r => r.Notes),
    };
}
