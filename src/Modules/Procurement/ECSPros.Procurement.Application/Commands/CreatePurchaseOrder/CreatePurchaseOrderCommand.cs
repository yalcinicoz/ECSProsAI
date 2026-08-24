using ECSPros.Procurement.Application.Services;
using ECSPros.Procurement.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.CreatePurchaseOrder;

public record CreatePurchaseOrderCommand(
    Guid SupplierId,
    DateTime? OrderDate,
    DateTime? ExpectedDate,
    string? Notes) : IRequest<Result<Guid>>;

public class CreatePurchaseOrderCommandHandler(IProcurementDbContext db)
    : IRequestHandler<CreatePurchaseOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePurchaseOrderCommand request, CancellationToken ct)
    {
        var prefix = $"SA-{DateTime.UtcNow:yyyyMMdd}-";
        var count = await db.PurchaseOrders.CountAsync(p => p.Code.StartsWith(prefix), ct);
        var po = new PurchaseOrder
        {
            Code = $"{prefix}{count + 1:D4}",
            SupplierId = request.SupplierId,
            OrderDate = request.OrderDate ?? DateTime.UtcNow,
            ExpectedDate = request.ExpectedDate,
            Notes = request.Notes,
            Status = "draft",
        };
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync(ct);
        return Result.Success(po.Id);
    }
}
