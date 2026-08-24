using ECSPros.Procurement.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Queries.GetPurchaseOrderDetail;

public record GetPurchaseOrderDetailQuery(Guid Id) : IRequest<Result<PurchaseOrderDetailDto>>;

public record PurchaseOrderItemDto(
    Guid Id, Guid? VariantId, string? ModelText, string? ColorText, string? SizeText,
    decimal Quantity, decimal UnitPrice, decimal Total, string? Notes, int SortOrder);

public record PurchaseOrderDetailDto(
    Guid Id, string Code, Guid SupplierId, DateTime OrderDate, DateTime? ExpectedDate,
    string Status, string? Notes, decimal TotalQuantity, decimal TotalAmount,
    List<PurchaseOrderItemDto> Items);

public class GetPurchaseOrderDetailQueryHandler(IProcurementDbContext db)
    : IRequestHandler<GetPurchaseOrderDetailQuery, Result<PurchaseOrderDetailDto>>
{
    public async Task<Result<PurchaseOrderDetailDto>> Handle(GetPurchaseOrderDetailQuery request, CancellationToken ct)
    {
        var po = await db.PurchaseOrders.AsNoTracking()
            .Include(p => p.Items.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (po is null) return Result.Failure<PurchaseOrderDetailDto>("Satın alma bulunamadı.");

        var items = po.Items.OrderBy(i => i.SortOrder)
            .Select(i => new PurchaseOrderItemDto(i.Id, i.VariantId, i.ModelText, i.ColorText, i.SizeText,
                i.Quantity, i.UnitPrice, i.Quantity * i.UnitPrice, i.Notes, i.SortOrder))
            .ToList();
        return Result.Success(new PurchaseOrderDetailDto(
            po.Id, po.Code, po.SupplierId, po.OrderDate, po.ExpectedDate, po.Status, po.Notes,
            items.Sum(i => i.Quantity), items.Sum(i => i.Total), items));
    }
}
