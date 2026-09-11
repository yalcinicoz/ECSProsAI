using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetInvoices;

/// <summary>Faturalar DataGrid şeması (F4): beyaz listeli sıralama/filtre + adlandırılmış filtreler + global arama; liste ve export aynı modeli kullanır.</summary>
public static class InvoiceGrid
{
    public static readonly string[] Statuses = { "created", "cancelled" };
    public static readonly string[] Types = { "e_archive", "e_invoice", "export" };
    public static readonly string[] Sources = { "internal", "erp", "marketplace", "integrator" };

    public static readonly GridSchema<Invoice> Schema = new GridSchema<Invoice>()
        .Kanal(i => i.Order.FirmPlatformId)   // Y3 (K2): faturanın kanalı SİPARİŞTEN gelir
        .Text("invoiceNumber", i => i.InvoiceNumber)
        .Text("recipient", i => i.RecipientName)
        .Text("taxNumber", i => i.RecipientTaxNumber)
        .Text("externalDocumentId", i => i.ExternalDocumentId)
        .Text("orderNumber", i => i.Order.OrderNumber)
        .Text("currency", i => i.Order.InvoiceCurrencyCode)
        .Text("erpReference", i => i.ErpReference)
        .Enum("erpStatus", i => i.ErpStatus)
        .Number("subtotal", i => i.Subtotal)
        .Number("taxBase", i => i.Subtotal - i.TotalDiscount)
        .Number("totalTax", i => i.TotalTax)
        .Enum("status", i => i.Status, Statuses)
        .Enum("invoiceType", i => i.InvoiceType, Types)
        .Enum("numberSource", i => i.NumberSource, Sources)
        .Enum("integratorStatus", i => i.IntegratorStatus)
        .Date("invoiceDate", i => i.InvoiceDate)
        .Date("createdAt", i => i.CreatedAt)
        .Number("total", i => i.GrandTotal)
        .Bool("hasPdf", i => i.IntegratorInvoiceUrl != null && i.IntegratorInvoiceUrl != "")
        .Guid("orderId", i => i.OrderId)
        .Sort("integratorStatus", i => i.IntegratorStatus)
        .Sort("orderNumber", i => i.Order.OrderNumber)
        .Sort("currency", i => i.Order.InvoiceCurrencyCode)
        .Sort("erpStatus", i => i.ErpStatus)
        .Sort("subtotal", i => i.Subtotal)
        .Sort("taxBase", i => i.Subtotal - i.TotalDiscount)
        .Sort("totalTax", i => i.TotalTax)
        .Sort("hasPdf", i => i.IntegratorInvoiceUrl != null && i.IntegratorInvoiceUrl != "")
        .Sort("invoiceNumber", i => i.InvoiceNumber)
        .Sort("recipient", i => i.RecipientName)
        .Sort("total", i => i.GrandTotal)
        .Sort("status", i => i.Status)
        .Sort("invoiceType", i => i.InvoiceType)
        .Sort("invoiceDate", i => i.InvoiceDate)
        .Sort("createdAt", i => i.CreatedAt)
        .DefaultSort(i => i.CreatedAt, desc: true)
        .TieBreaker(i => i.Id);

    public static IQueryable<Invoice> ApplyNamed(IQueryable<Invoice> query, InvoiceListFilters f, bool includeStatus = true)
    {
        if (f.OrderId.HasValue) query = query.Where(i => i.OrderId == f.OrderId.Value);
        if (includeStatus && !string.IsNullOrEmpty(f.Status)) query = query.Where(i => i.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            query = query.Where(i =>
                i.InvoiceNumber.ToLower().Contains(term) ||
                i.RecipientName.ToLower().Contains(term) ||
                (i.RecipientTaxNumber != null && i.RecipientTaxNumber.Contains(term)) ||
                (i.ExternalDocumentId != null && i.ExternalDocumentId.ToLower().Contains(term)));
        }
        return query;
    }

    public static IQueryable<Invoice> ApplyAll(IQueryable<Invoice> query, InvoiceListFilters f, GridRequest? grid, bool includeStatus = true)
    {
        query = ApplyNamed(query, f, includeStatus);
        // Y3 (K2): kanal kapsamı — kapsam dışı satır listede/sayımda/exportta görünmez.
        return Schema.ApplyKanalKapsami(includeStatus ? Schema.ApplyFilters(query, grid) : Schema.ApplyFilters(query, grid, "status"), grid?.KanalKisiti);
    }

    public static string StatusLabel(string s) => s switch { "created" => "Oluşturuldu", "cancelled" => "İptal", _ => s };
    public static string TypeLabel(string s) => s switch { "e_archive" => "e-Arşiv", "e_invoice" => "e-Fatura", "export" => "İhracat", _ => s };
    public static string ErpLabel(string s) => s switch { "not_applicable" or "" => "Gönderim yok", "pending" => "Bekliyor", "sent" => "Gönderildi", "acknowledged" => "ERP kesti", "error" => "Hata", _ => s };
    public static string SourceLabel(string s) => s switch { "internal" => "Bizim seri", "erp" => "ERP", "marketplace" => "Pazaryeri", "integrator" => "Entegratör", _ => s };
}

public record InvoiceListFilters(Guid? OrderId = null, string? Status = null, string? Search = null);

public record InvoiceExportRow(
    string InvoiceNumber, string InvoiceType, string NumberSource, DateTime InvoiceDate, DateTime CreatedAt, string RecipientName,
    string? RecipientCompanyName, string? RecipientTaxNumber, string? RecipientTaxOffice, string RecipientAddress,
    decimal Subtotal, decimal TotalDiscount, decimal TotalTax, decimal GrandTotal, string Status, string IntegratorStatus,
    string? ExternalDocumentId, string? ExternalSource, bool HasPdf, string OrderNumber,
    Guid? Ettn = null, string CurrencyCode = "TRY", DateTime? IntegratorSentAt = null,
    string ErpStatus = "", DateTime? ErpSentAt = null, string? ErpReference = null);

public record ExportInvoicesQuery(InvoiceListFilters Filters, GridRequest Grid, int MaxRows) : IRequest<Result<GridExportSource<InvoiceExportRow>>>;

public class ExportInvoicesQueryHandler(IOrderDbContext db) : IRequestHandler<ExportInvoicesQuery, Result<GridExportSource<InvoiceExportRow>>>
{
    public async Task<Result<GridExportSource<InvoiceExportRow>>> Handle(ExportInvoicesQuery r, CancellationToken ct)
    {
        var q = InvoiceGrid.ApplyAll(db.Invoices.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<InvoiceExportRow>>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = InvoiceGrid.Schema.ApplySort(q, r.Grid).Select(i => new InvoiceExportRow(
            i.InvoiceNumber, i.InvoiceType, i.NumberSource, i.InvoiceDate, i.CreatedAt, i.RecipientName,
            i.RecipientCompanyName, i.RecipientTaxNumber, i.RecipientTaxOffice, i.RecipientAddress,
            i.Subtotal, i.TotalDiscount, i.TotalTax, i.GrandTotal, i.Status, i.IntegratorStatus,
            i.ExternalDocumentId, i.ExternalSource, i.IntegratorInvoiceUrl != null && i.IntegratorInvoiceUrl != "", i.Order.OrderNumber,
            i.Ettn, i.Order.InvoiceCurrencyCode, i.IntegratorSentAt, i.ErpStatus, i.ErpSentAt, i.ErpReference));
        return Result.Success(new GridExportSource<InvoiceExportRow>(count, rows));
    }
}
