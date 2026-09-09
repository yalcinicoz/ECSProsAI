using ECSPros.Procurement.Application.Services;
using ECSPros.Procurement.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Queries.GetReceiptBatches;

/// <summary>
/// Mal kabul partileri DataGrid şeması (2026-09-09): beyaz listeli filtre/sıralama + adlandırılmış
/// filtreler (supplierId/status) + global arama (kod, irsaliye no).
///
/// <para>Tedarikçi ve depo ADLARI başka modüllerde (Accounts / Inventory) çözülüyor → o kolonlar
/// SIRALANAMAZ; süzgeçleri adlandırılmış <c>supplierId</c> ve şemadaki <c>warehouseId</c> ile yapılır
/// (aynı kalıp: Satın Almalar tedarikçi, Stoklar ürün kodu).</para>
/// </summary>
public static class ReceiptBatchGrid
{
    public static readonly GridSchema<ReceiptBatch> Schema = new GridSchema<ReceiptBatch>()
        .Text("code", b => b.Code)
        .Text("deliveryNoteNumber", b => b.DeliveryNoteNumber)
        .Text("notes", b => b.Notes)
        .Enum("status", b => b.Status, ReceiptBatch.Statuses)
        .Bool("hasInvoice", b => b.SupplierInvoiceId != null)
        .Bool("open", b => b.Status != "completed")
        .Number("packageCount", b => b.PackageCount)
        .Number("itemCount", b => b.Items.Count(i => !i.IsDeleted))
        .Number("linkedPoCount", b => b.PurchaseOrders.Count(x => !x.IsDeleted))
        .Date("receivedAt", b => b.ReceivedAt)
        .Date("createdAt", b => b.CreatedAt)
        .Guid("supplierId", b => b.SupplierId)
        .Guid("warehouseId", b => b.WarehouseId)
        .Sort("code", b => b.Code)
        .Sort("deliveryNoteNumber", b => b.DeliveryNoteNumber)
        .Sort("status", b => b.Status)
        .Sort("packageCount", b => b.PackageCount)
        .Sort("itemCount", b => b.Items.Count(i => !i.IsDeleted))
        .Sort("linkedPoCount", b => b.PurchaseOrders.Count(x => !x.IsDeleted))
        .Sort("hasInvoice", b => b.SupplierInvoiceId != null)
        .Sort("receivedAt", b => b.ReceivedAt)
        .Sort("createdAt", b => b.CreatedAt)
        .DefaultSort(b => b.Code, desc: true)
        .TieBreaker(b => b.Id);

    public static IQueryable<ReceiptBatch> ApplyNamed(IQueryable<ReceiptBatch> query, ReceiptBatchFilters f)
    {
        if (f.SupplierId.HasValue) query = query.Where(b => b.SupplierId == f.SupplierId.Value);
        if (!string.IsNullOrWhiteSpace(f.Status)) query = query.Where(b => b.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim().ToLower();
            query = query.Where(b => b.Code.ToLower().Contains(s)
                || (b.DeliveryNoteNumber ?? "").ToLower().Contains(s));
        }
        return query;
    }

    public static IQueryable<ReceiptBatch> ApplyAll(IQueryable<ReceiptBatch> query, ReceiptBatchFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    public static string StatusLabel(string s) => s switch
    {
        "received" => "Teslim Alındı", "sorting" => "Ayrıştırmada", "completed" => "Tamamlandı", _ => s,
    };
}

public record ReceiptBatchFilters(Guid? SupplierId = null, string? Status = null, string? Search = null);

public record ReceiptBatchExportRow(
    string Code, DateTime ReceivedAt, int? PackageCount, string? DeliveryNoteNumber, string Status,
    int ItemCount, int LinkedPoCount, bool HasInvoice, string? Notes);

public record ExportReceiptBatchesQuery(ReceiptBatchFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<ReceiptBatchExportRow>>>;

public class ExportReceiptBatchesQueryHandler(IProcurementDbContext db)
    : IRequestHandler<ExportReceiptBatchesQuery, Result<GridExportSource<ReceiptBatchExportRow>>>
{
    public async Task<Result<GridExportSource<ReceiptBatchExportRow>>> Handle(ExportReceiptBatchesQuery r, CancellationToken ct)
    {
        var q = ReceiptBatchGrid.ApplyAll(db.ReceiptBatches.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<ReceiptBatchExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = ReceiptBatchGrid.Schema.ApplySort(q, r.Grid).Select(b => new ReceiptBatchExportRow(
            b.Code, b.ReceivedAt, b.PackageCount, b.DeliveryNoteNumber, b.Status,
            b.Items.Count(i => !i.IsDeleted), b.PurchaseOrders.Count(x => !x.IsDeleted),
            b.SupplierInvoiceId != null, b.Notes));
        return Result.Success(new GridExportSource<ReceiptBatchExportRow>(count, rows));
    }
}
