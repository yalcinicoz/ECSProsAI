using ECSPros.Procurement.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Queries.GetReceiptBatches;

public record GetReceiptBatchesQuery(
    Guid? SupplierId = null,
    string? Status = null,
    string? Search = null,        // kod veya irsaliye no
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<ReceiptBatchRowDto>>>;

public record ReceiptBatchRowDto(
    Guid Id, string Code, Guid SupplierId, Guid WarehouseId, DateTime ReceivedAt,
    int? PackageCount, string? DeliveryNoteNumber, string Status,
    int ItemCount, int LinkedPoCount, bool HasInvoice, string? Notes);

public class GetReceiptBatchesQueryHandler(IProcurementDbContext db)
    : IRequestHandler<GetReceiptBatchesQuery, Result<PagedResult<ReceiptBatchRowDto>>>
{
    public async Task<Result<PagedResult<ReceiptBatchRowDto>>> Handle(GetReceiptBatchesQuery request, CancellationToken ct)
    {
        var q = db.ReceiptBatches.AsNoTracking();
        if (request.SupplierId.HasValue) q = q.Where(b => b.SupplierId == request.SupplierId.Value);
        if (!string.IsNullOrWhiteSpace(request.Status)) q = q.Where(b => b.Status == request.Status);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLower();
            q = q.Where(b => b.Code.ToLower().Contains(s) || (b.DeliveryNoteNumber ?? "").ToLower().Contains(s));
        }

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(b => b.Code)
            .Skip((Math.Max(1, request.Page) - 1) * request.PageSize).Take(request.PageSize)
            .Select(b => new ReceiptBatchRowDto(
                b.Id, b.Code, b.SupplierId, b.WarehouseId, b.ReceivedAt, b.PackageCount, b.DeliveryNoteNumber,
                b.Status, b.Items.Count(i => !i.IsDeleted), b.PurchaseOrders.Count(x => !x.IsDeleted),
                b.SupplierInvoiceId != null, b.Notes))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<ReceiptBatchRowDto>(rows, total, request.Page, request.PageSize));
    }
}
