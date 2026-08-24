using ECSPros.Procurement.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.DeletePurchaseOrderItem;

public record DeletePurchaseOrderItemCommand(Guid PurchaseOrderId, Guid ItemId) : IRequest<Result<bool>>;

public class DeletePurchaseOrderItemCommandHandler(IProcurementDbContext db)
    : IRequestHandler<DeletePurchaseOrderItemCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeletePurchaseOrderItemCommand request, CancellationToken ct)
    {
        var po = await db.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId, ct);
        if (po is null) return Result.Failure<bool>("Satın alma bulunamadı.");
        if (po.Status is "closed" or "cancelled") return Result.Failure<bool>("Kapalı/iptal satın almadan kalem silinemez.");
        var item = await db.PurchaseOrderItems
            .FirstOrDefaultAsync(i => i.Id == request.ItemId && i.PurchaseOrderId == request.PurchaseOrderId, ct);
        if (item is null) return Result.Failure<bool>("Kalem bulunamadı.");
        item.IsDeleted = true;
        item.DeletedAt = DateTime.UtcNow;
        po.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
