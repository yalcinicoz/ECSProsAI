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
        new("recipient", "Alıcı", r => r.RecipientName),
        new("companyName", "Firma", r => r.RecipientCompanyName),
        new("taxNumber", "Vergi No / TCKN", r => r.RecipientTaxNumber),
        new("taxOffice", "Vergi Dairesi", r => r.RecipientTaxOffice),
        new("address", "Adres", r => r.RecipientAddress),
        new("subtotal", "Ara Toplam", r => r.Subtotal),
        new("totalDiscount", "İndirim", r => r.TotalDiscount),
        new("totalTax", "KDV", r => r.TotalTax),
        new("total", "Genel Toplam", r => r.GrandTotal),
        new("integratorStatus", "Entegratör Durumu", r => r.IntegratorStatus),
        new("hasPdf", "PDF", r => r.HasPdf ? "Var" : "Yok"),
        new("externalDocumentId", "Dış Belge No", r => r.ExternalDocumentId),
        new("externalSource", "Dış Kaynak", r => r.ExternalSource),
        new("createdAt", "Kayıt Tarihi", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
