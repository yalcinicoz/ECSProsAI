using ECSPros.Procurement.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.UpdatePurchaseOrder;

public record UpdatePurchaseOrderCommand(
    Guid Id,
    DateTime? OrderDate,
    DateTime? ExpectedDate,
    string? Notes) : IRequest<Result<bool>>;

public class UpdatePurchaseOrderCommandHandler(IProcurementDbContext db)
    : IRequestHandler<UpdatePurchaseOrderCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdatePurchaseOrderCommand request, CancellationToken ct)
    {
        var po = await db.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (po is null) return Result.Failure<bool>("Satın alma bulunamadı.");
        if (po.Status is "closed" or "cancelled") return Result.Failure<bool>("Kapalı/iptal satın alma düzenlenemez.");
        if (request.OrderDate.HasValue) po.OrderDate = request.OrderDate.Value;
        po.ExpectedDate = request.ExpectedDate;
        po.Notes = request.Notes;
        po.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
