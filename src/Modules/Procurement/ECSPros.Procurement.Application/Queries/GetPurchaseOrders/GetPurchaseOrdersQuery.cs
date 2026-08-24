using ECSPros.Procurement.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Queries.GetPurchaseOrders;

public record GetPurchaseOrdersQuery(
    Guid? SupplierId = null,
    string? Status = null,
    string? Search = null,        // kod veya kalem model metni
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<PurchaseOrderRowDto>>>;

public record PurchaseOrderRowDto(
    Guid Id, string Code, Guid SupplierId, DateTime OrderDate, DateTime? ExpectedDate,
    string Status, int ItemCount, decimal TotalQuantity, decimal TotalAmount, string? Notes);

public class GetPurchaseOrdersQueryHandler(IProcurementDbContext db)
    : IRequestHandler<GetPurchaseOrdersQuery, Result<PagedResult<PurchaseOrderRowDto>>>
{
    public async Task<Result<PagedResult<PurchaseOrderRowDto>>> Handle(GetPurchaseOrdersQuery request, CancellationToken ct)
    {
        var q = db.PurchaseOrders.AsNoTracking();
        if (request.SupplierId.HasValue) q = q.Where(p => p.SupplierId == request.SupplierId.Value);
        if (!string.IsNullOrWhiteSpace(request.Status)) q = q.Where(p => p.Status == request.Status);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLower();
            q = q.Where(p => p.Code.ToLower().Contains(s)
                || p.Items.Any(i => !i.IsDeleted && (
                    (i.ModelText ?? "").ToLower().Contains(s) || (i.ColorText ?? "").ToLower().Contains(s))));
        }

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(p => p.Code)
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
