using ECSPros.Procurement.Application.Services;
using ECSPros.Procurement.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Queries.GetPurchaseOrders;

/// <summary>
/// Satın alma siparişleri DataGrid şeması (2026-09-09): beyaz listeli filtre/sıralama + adlandırılmış
/// filtreler (supplierId/status) + global arama (kod, kalem model/renk metni).
///
/// <para>Kalem sayısı / toplam adet / toplam tutar hesaplanmış alanlardır (kalem koleksiyonundan);
/// hem filtre hem sıralama olarak bildirildi — alt sorgu ile SQL'e çevrilir.</para>
/// <para>Tedarikçi ADI başka modülde (Accounts) → bu şemadan sıralanamaz; ekran tedarikçiyi
/// adlandırılmış <c>supplierId</c> süzgeciyle filtreler.</para>
/// </summary>
public static class PurchaseOrderGrid
{
    public static readonly GridSchema<PurchaseOrder> Schema = new GridSchema<PurchaseOrder>()
        .Text("code", p => p.Code)
        .Text("notes", p => p.Notes)
        .Enum("status", p => p.Status, PurchaseOrder.Statuses)
        .Bool("open", p => p.Status != "closed" && p.Status != "cancelled")
        .Bool("overdue", p => p.ExpectedDate != null && p.ExpectedDate < DateTime.UtcNow
            && p.Status != "closed" && p.Status != "cancelled")
        .Number("itemCount", p => p.Items.Count(i => !i.IsDeleted))
        .Number("totalQuantity", p => p.Items.Where(i => !i.IsDeleted).Sum(i => (decimal?)i.Quantity) ?? 0)
        .Number("totalAmount", p => p.Items.Where(i => !i.IsDeleted).Sum(i => (decimal?)(i.Quantity * i.UnitPrice)) ?? 0)
        .Date("orderDate", p => p.OrderDate)
        .Date("expectedDate", p => p.ExpectedDate)
        .Date("createdAt", p => p.CreatedAt)
        .Guid("supplierId", p => p.SupplierId)
        .Sort("code", p => p.Code)
        .Sort("status", p => p.Status)
        .Sort("itemCount", p => p.Items.Count(i => !i.IsDeleted))
        .Sort("totalQuantity", p => p.Items.Where(i => !i.IsDeleted).Sum(i => (decimal?)i.Quantity) ?? 0)
        .Sort("totalAmount", p => p.Items.Where(i => !i.IsDeleted).Sum(i => (decimal?)(i.Quantity * i.UnitPrice)) ?? 0)
        .Sort("orderDate", p => p.OrderDate)
        .Sort("expectedDate", p => p.ExpectedDate)
        .Sort("createdAt", p => p.CreatedAt)
        .DefaultSort(p => p.Code, desc: true)
        .TieBreaker(p => p.Id);

    public static IQueryable<PurchaseOrder> ApplyNamed(IQueryable<PurchaseOrder> query, PurchaseOrderFilters f)
    {
        if (f.SupplierId.HasValue) query = query.Where(p => p.SupplierId == f.SupplierId.Value);
        if (!string.IsNullOrWhiteSpace(f.Status)) query = query.Where(p => p.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim().ToLower();
            query = query.Where(p => p.Code.ToLower().Contains(s)
                || p.Items.Any(i => !i.IsDeleted && (
                    (i.ModelText ?? "").ToLower().Contains(s) || (i.ColorText ?? "").ToLower().Contains(s))));
        }
        return query;
    }

    public static IQueryable<PurchaseOrder> ApplyAll(IQueryable<PurchaseOrder> query, PurchaseOrderFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    public static string StatusLabel(string s) => s switch
    {
        "draft" => "Taslak", "ordered" => "Sipariş Verildi", "receiving" => "Mal Kabulde",
        "closed" => "Kapandı", "cancelled" => "İptal", _ => s,
    };
}

public record PurchaseOrderFilters(Guid? SupplierId = null, string? Status = null, string? Search = null);

public record PurchaseOrderExportRow(
    string Code, DateTime OrderDate, DateTime? ExpectedDate, string Status,
    int ItemCount, decimal TotalQuantity, decimal TotalAmount, string? Notes);

public record ExportPurchaseOrdersQuery(PurchaseOrderFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<PurchaseOrderExportRow>>>;

public class ExportPurchaseOrdersQueryHandler(IProcurementDbContext db)
    : IRequestHandler<ExportPurchaseOrdersQuery, Result<GridExportSource<PurchaseOrderExportRow>>>
{
    public async Task<Result<GridExportSource<PurchaseOrderExportRow>>> Handle(ExportPurchaseOrdersQuery r, CancellationToken ct)
    {
        var q = PurchaseOrderGrid.ApplyAll(db.PurchaseOrders.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<PurchaseOrderExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = PurchaseOrderGrid.Schema.ApplySort(q, r.Grid).Select(p => new PurchaseOrderExportRow(
            p.Code, p.OrderDate, p.ExpectedDate, p.Status,
            p.Items.Count(i => !i.IsDeleted),
            p.Items.Where(i => !i.IsDeleted).Sum(i => (decimal?)i.Quantity) ?? 0,
            p.Items.Where(i => !i.IsDeleted).Sum(i => (decimal?)(i.Quantity * i.UnitPrice)) ?? 0,
            p.Notes));
        return Result.Success(new GridExportSource<PurchaseOrderExportRow>(count, rows));
    }
}
