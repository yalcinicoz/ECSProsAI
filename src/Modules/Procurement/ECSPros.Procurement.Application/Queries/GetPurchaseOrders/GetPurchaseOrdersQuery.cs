using ECSPros.Procurement.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Queries.GetPurchaseOrders;

public record GetPurchaseOrdersQuery(
    Guid? SupplierId = null,
    string? Status = null,
    string? Search = null,        // kod veya kalem model metni
    int Page = 1,
    int PageSize = 20,
    GridRequest? Grid = null) : IRequest<Result<PagedResult<PurchaseOrderRowDto>>>;
    // Grid (2026-09-09, DataGrid): beyaz listeli f.* filtreleri + sort/dir (PurchaseOrderGrid.Schema)

public record PurchaseOrderRowDto(
    Guid Id, string Code, Guid SupplierId, DateTime OrderDate, DateTime? ExpectedDate,
    string Status, int ItemCount, decimal TotalQuantity, decimal TotalAmount, string? Notes);

public class GetPurchaseOrdersQueryHandler(IProcurementDbContext db)
    : IRequestHandler<GetPurchaseOrdersQuery, Result<PagedResult<PurchaseOrderRowDto>>>
{
    public async Task<Result<PagedResult<PurchaseOrderRowDto>>> Handle(GetPurchaseOrdersQuery request, CancellationToken ct)
    {
        // Adlandırılmış + grid filtreleri TEK yerden (liste ve Excel aynı sonucu verir).
        var q = PurchaseOrderGrid.ApplyAll(
            db.PurchaseOrders.AsNoTracking(),
            new PurchaseOrderFilters(request.SupplierId, request.Status, request.Search),
            request.Grid);

        var total = await q.CountAsync(ct);
        var rows = await PurchaseOrderGrid.Schema.ApplySort(q, request.Grid)
            .Skip((Math.Max(1, request.Page) - 1) * request.PageSize).Take(request.PageSize)
            .Select(p => new PurchaseOrderRowDto(
                p.Id, p.Code, p.SupplierId, p.OrderDate, p.ExpectedDate, p.Status,
                p.Items.Count(i => !i.IsDeleted),
                p.Items.Where(i => !i.IsDeleted).Sum(i => (decimal?)i.Quantity) ?? 0,
                p.Items.Where(i => !i.IsDeleted).Sum(i => (decimal?)(i.Quantity * i.UnitPrice)) ?? 0,
                p.Notes))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<PurchaseOrderRowDto>(rows, total, request.Page, request.PageSize));
    }
}
