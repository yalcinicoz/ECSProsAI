using ECSPros.Finance.Application.Services;
using ECSPros.Finance.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Finance.Application.Queries.GetSupplierInvoices;

/// <summary>
/// Tedarikçi faturaları DataGrid şeması: beyaz listeli filtre/sıralama + adlandırılmış filtreler (currentAccountId, status) + global arama (fatura no, not).
/// invoiceDate/dueDate DateOnly kolonlardır (GridSchema.Date gün karşılaştırması yapar).
/// </summary>
public static class SupplierInvoiceGrid
{
    public static readonly string[] Statuses = { "draft", "open", "partial", "paid", "cancelled" };

    public static readonly GridSchema<SupplierInvoice> Schema = new GridSchema<SupplierInvoice>()
        .Text("invoiceNumber", i => i.InvoiceNumber)
        .Text("notes", i => i.Notes)
        .Enum("status", i => i.Status, Statuses)
        .Number("total", i => i.GrandTotal)
        .Number("subtotal", i => i.Subtotal)
        .Number("totalTax", i => i.TotalTax)
        .Number("itemCount", i => i.Items.Count)
        .Date("createdAt", i => i.CreatedAt)
        .Date("invoiceDate", i => i.InvoiceDate)
        .Date("dueDate", i => i.DueDate)
        .Guid("currentAccountId", i => i.CurrentAccountId)
        .Sort("invoiceNumber", i => i.InvoiceNumber)
        .Sort("invoiceDate", i => i.InvoiceDate)
        .Sort("dueDate", i => i.DueDate)
        .Sort("itemCount", i => i.Items.Count)
        .Sort("total", i => i.GrandTotal)
        .Sort("status", i => i.Status)
        .Sort("createdAt", i => i.CreatedAt)
        .DefaultSort(i => i.InvoiceDate, desc: true)
        .TieBreaker(i => i.Id);

    public static IQueryable<SupplierInvoice> ApplyNamed(IQueryable<SupplierInvoice> query, SupplierInvoiceListFilters f)
    {
        if (f.CurrentAccountId.HasValue) query = query.Where(i => i.CurrentAccountId == f.CurrentAccountId.Value);
        if (!string.IsNullOrWhiteSpace(f.Status)) query = query.Where(i => i.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            query = query.Where(i => i.InvoiceNumber.ToLower().Contains(term) || (i.Notes != null && i.Notes.ToLower().Contains(term)));
        }
        return query;
    }

    public static IQueryable<SupplierInvoice> ApplyAll(IQueryable<SupplierInvoice> query, SupplierInvoiceListFilters f, GridRequest? grid)
    {
        query = ApplyNamed(query, f);
        return Schema.ApplyFilters(query, grid);
    }

    public static string StatusLabel(string s) => s switch
    {
        "draft" => "Taslak", "open" => "Açık", "partial" => "Kısmi Ödendi", "paid" => "Ödendi", "cancelled" => "İptal", _ => s,
    };
}

public record SupplierInvoiceListFilters(Guid? CurrentAccountId = null, string? Status = null, string? Search = null);

public record SupplierInvoiceExportRow(
    string InvoiceNumber, Guid CurrentAccountId, DateOnly InvoiceDate, DateOnly? DueDate, string Status, int ItemCount,
    decimal Subtotal, decimal TotalDiscount, decimal TotalTax, decimal GrandTotal, string? Notes, DateTime CreatedAt);

public record ExportSupplierInvoicesQuery(SupplierInvoiceListFilters Filters, GridRequest Grid, int MaxRows) : IRequest<Result<GridExportSource<SupplierInvoiceExportRow>>>;

public class ExportSupplierInvoicesQueryHandler(IFinanceDbContext db) : IRequestHandler<ExportSupplierInvoicesQuery, Result<GridExportSource<SupplierInvoiceExportRow>>>
{
    public async Task<Result<GridExportSource<SupplierInvoiceExportRow>>> Handle(ExportSupplierInvoicesQuery r, CancellationToken ct)
    {
        var q = SupplierInvoiceGrid.ApplyAll(db.SupplierInvoices.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<SupplierInvoiceExportRow>>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = SupplierInvoiceGrid.Schema.ApplySort(q, r.Grid).Select(i => new SupplierInvoiceExportRow(
            i.InvoiceNumber, i.CurrentAccountId, i.InvoiceDate, i.DueDate, i.Status, i.Items.Count,
            i.Subtotal, i.TotalDiscount, i.TotalTax, i.GrandTotal, i.Notes, i.CreatedAt));
        return Result.Success(new GridExportSource<SupplierInvoiceExportRow>(count, rows));
    }
}
