using ECSPros.Order.Application.Queries.GetQuotes;

namespace ECSPros.Api.Grid;

/// <summary>Teklifler Excel kolonları (teklif no kilitli).</summary>
public static class QuoteExportColumns
{
    private static object? Tarih(DateTime? d) => d.HasValue ? GridExportWriter.ToIstanbul(d.Value) : null;

    public static readonly IReadOnlyList<GridExportColumn<QuoteExportRow>> All = new GridExportColumn<QuoteExportRow>[]
    {
        new("quoteNumber", "Teklif No", r => r.QuoteNumber, Locked: true),
        new("status", "Durum", r => QuoteGrid.StatusLabel(r.Status)),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
        new("validUntil", "Geçerlilik", r => GridExportWriter.ToIstanbul(r.ValidUntil)),
        new("sentAt", "Gönderim", r => Tarih(r.SentAt)),
        new("viewedAt", "Görüntülenme", r => Tarih(r.ViewedAt)),
        new("respondedAt", "Yanıt", r => Tarih(r.RespondedAt)),
        new("subtotal", "Ara Toplam", r => r.Subtotal),
        new("totalDiscount", "İndirim", r => r.TotalDiscount),
        new("totalTax", "KDV", r => r.TotalTax),
        new("total", "Tutar", r => r.GrandTotal),
        new("currencyCode", "Para Birimi", r => r.CurrencyCode),
        new("converted", "Siparişe Dönüştü", r => r.Converted ? "Evet" : "Hayır"),
        new("memberId", "Üye Id", r => r.MemberId),
        new("notesToCustomer", "Müşteri Notu", r => r.NotesToCustomer),
    };
}
