using ECSPros.Order.Application.Queries.GetInvoices;

namespace ECSPros.Api.Grid;

/// <summary>Faturalar Excel kolonları (anahtarlar panel DataGrid kolonlarıyla aynı; fatura no + durum kilitli).</summary>
public static class InvoiceExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<InvoiceExportRow>> All = new GridExportColumn<InvoiceExportRow>[]
    {
        new("invoiceNumber", "Fatura No", r => r.InvoiceNumber, Locked: true),
        new("invoiceDate", "Fatura Tarihi", r => GridExportWriter.ToIstanbul(r.InvoiceDate)),
        new("invoiceType", "Tip", r => InvoiceGrid.TypeLabel(r.InvoiceType)),
        new("numberSource", "Numara Kaynağı", r => InvoiceGrid.SourceLabel(r.NumberSource)),
        new("status", "Durum", r => InvoiceGrid.StatusLabel(r.Status), Locked: true),
        new("orderNumber", "Sipariş No", r => r.OrderNumber),
        new("ettn", "ETTN", r => r.Ettn?.ToString()),
        new("currency", "Para Birimi", r => r.CurrencyCode),
        new("recipient", "Alıcı", r => r.RecipientName),
        new("companyName", "Firma", r => r.RecipientCompanyName),
        new("taxNumber", "Vergi No / TCKN", r => r.RecipientTaxNumber),
        new("taxOffice", "Vergi Dairesi", r => r.RecipientTaxOffice),
        new("address", "Adres", r => r.RecipientAddress, AlanYetkisi: "address"),
        new("subtotal", "Ara Toplam", r => r.Subtotal),
        new("totalDiscount", "İndirim", r => r.TotalDiscount),
        new("totalTax", "KDV", r => r.TotalTax),
        new("taxBase", "Vergi Matrahı", r => r.Subtotal - r.TotalDiscount),
        new("total", "Ödenecek Tutar", r => r.GrandTotal),
        new("integratorStatus", "Entegratör Durumu", r => r.IntegratorStatus),
        new("integratorSentAt", "Entegratöre Gönderim", r => r.IntegratorSentAt is { } d ? GridExportWriter.ToIstanbul(d) : null),
        new("erpStatus", "ERP Durumu", r => InvoiceGrid.ErpLabel(r.ErpStatus)),
        new("erpSentAt", "ERP Gönderim", r => r.ErpSentAt is { } d ? GridExportWriter.ToIstanbul(d) : null),
        new("erpReference", "ERP Referansı", r => r.ErpReference),
        new("hasPdf", "PDF", r => r.HasPdf ? "Var" : "Yok"),
        new("externalDocumentId", "Dış Belge No", r => r.ExternalDocumentId),
        new("externalSource", "Dış Kaynak", r => r.ExternalSource),
        new("createdAt", "Kayıt Tarihi", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
