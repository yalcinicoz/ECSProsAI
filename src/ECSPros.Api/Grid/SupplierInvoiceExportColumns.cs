using ECSPros.Finance.Application.Queries.GetSupplierInvoices;

namespace ECSPros.Api.Grid;

/// <summary>Tedarikçi faturaları Excel kolonları (fatura no kilitli).</summary>
public static class SupplierInvoiceExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<SupplierInvoiceExportRow>> All = new GridExportColumn<SupplierInvoiceExportRow>[]
    {
        new("invoiceNumber", "Fatura No", r => r.InvoiceNumber, Locked: true),
        new("currentAccountId", "Cari Id", r => r.CurrentAccountId),
        new("invoiceDate", "Tarih", r => r.InvoiceDate.ToDateTime(TimeOnly.MinValue)),
        new("dueDate", "Vade", r => r.DueDate.HasValue ? (object)r.DueDate.Value.ToDateTime(TimeOnly.MinValue) : null),
        new("status", "Durum", r => SupplierInvoiceGrid.StatusLabel(r.Status)),
        new("itemCount", "Kalem", r => r.ItemCount),
        new("subtotal", "Ara Toplam", r => r.Subtotal),
        new("totalDiscount", "İndirim", r => r.TotalDiscount),
        new("totalTax", "KDV", r => r.TotalTax),
        new("total", "Tutar", r => r.GrandTotal),
        new("notes", "Not", r => r.Notes),
        new("createdAt", "Kayıt Tarihi", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
