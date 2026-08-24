using ECSPros.Procurement.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Queries.GetReceiptBatchDetail;

public record GetReceiptBatchDetailQuery(Guid Id) : IRequest<Result<ReceiptBatchDetailDto>>;

public record ReceiptBatchItemDto(Guid Id, string DescriptionText, decimal? Quantity, decimal? UnitPrice, int SortOrder);
public record LinkedPurchaseOrderDto(Guid Id, string Code, string Status, DateTime OrderDate, int ItemCount, decimal TotalQuantity, decimal TotalAmount);

public record ReceiptBatchDetailDto(
    Guid Id, string Code, Guid SupplierId, Guid WarehouseId, DateTime ReceivedAt,
    int? PackageCount, string? DeliveryNoteNumber, Guid? SupplierInvoiceId, string Status, string? Notes,
    List<ReceiptBatchItemDto> Items, List<LinkedPurchaseOrderDto> PurchaseOrders);

public class GetReceiptBatchDetailQueryHandler(IProcurementDbContext db)
    : IRequestHandler<GetReceiptBatchDetailQuery, Result<ReceiptBatchDetailDto>>
{
    public async Task<Result<ReceiptBatchDetailDto>> Handle(GetReceiptBatchDetailQuery request, CancellationToken ct)
    {
        var b = await db.ReceiptBatches.AsNoTracking()
            .Include(x => x.Items.Where(i => !i.IsDeleted))
            .Include(x => x.PurchaseOrders.Where(l => !l.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (b is null) return Result.Failure<ReceiptBatchDetailDto>("Parti bulunamadı.");

        var poIds = b.PurchaseOrders.Select(l => l.PurchaseOrderId).ToList();
        var pos = poIds.Count == 0 ? new() : await db.PurchaseOrders.AsNoTracking()
            .Where(p => poIds.Contains(p.Id))
            .Select(p => new LinkedPurchaseOrderDto(p.Id, p.Code, p.Status, p.OrderDate,
                p.Items.Count(i => !i.IsDeleted),
                p.Items.Where(i => !i.IsDeleted).Sum(i => (decimal?)i.Quantity) ?? 0,
                p.Items.Where(i => !i.IsDeleted).Sum(i => (decimal?)(i.Quantity * i.UnitPrice)) ?? 0))
            .ToListAsync(ct);

        return Result.Success(new ReceiptBatchDetailDto(
            b.Id, b.Code, b.SupplierId, b.WarehouseId, b.ReceivedAt, b.PackageCount, b.DeliveryNoteNumber,
            b.SupplierInvoiceId, b.Status, b.Notes,
            b.Items.OrderBy(i => i.SortOrder).Select(i => new ReceiptBatchItemDto(i.Id, i.DescriptionText, i.Quantity, i.UnitPrice, i.SortOrder)).ToList(),
            pos.OrderBy(p => p.Code).ToList()));
    }
}
