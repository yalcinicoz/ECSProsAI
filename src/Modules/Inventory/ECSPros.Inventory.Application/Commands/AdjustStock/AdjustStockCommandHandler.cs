using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Inventory.Domain.Events;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Commands.AdjustStock;

// Cutover (2026-07-14): elle stok düzeltmesi depo seviyesinde (StockOps ile raflara yansır).
// + delta → deponun rafına eklenir; − delta → deponun raflarından düşülür (negatife düşemez).
// NOT: belirli bir rafa düzeltme ileride BinId parametresiyle eklenebilir (komut+admin UI işi).
public class AdjustStockCommandHandler(IInventoryDbContext context, IPublisher publisher)
    : IRequestHandler<AdjustStockCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        var warehouseExists = await context.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, cancellationToken);
        if (!warehouseExists)
            return Result.Failure<Guid>("Depo bulunamadı.");

        if (request.QuantityDelta < 0)
        {
            var mevcut = await context.Stocks
                .Where(s => s.VariantId == request.VariantId && s.WarehouseId == request.WarehouseId)
                .SumAsync(s => (int?)s.Quantity, cancellationToken) ?? 0;
            if (mevcut + request.QuantityDelta < 0)
                return Result.Failure<Guid>("Stok miktarı negatife düşemez.");
            await StockOps.ConsumeAsync(context, request.VariantId, request.WarehouseId, -request.QuantityDelta, cancellationToken);
        }
        else if (request.QuantityDelta > 0)
        {
            await StockOps.ReceiveAsync(context, request.VariantId, request.WarehouseId, request.QuantityDelta, preferReturns: false, cancellationToken);
        }

        var movement = new StockMovement
        {
            VariantId = request.VariantId,
            ToWarehouseId = request.QuantityDelta > 0 ? request.WarehouseId : null,
            FromWarehouseId = request.QuantityDelta < 0 ? request.WarehouseId : null,
            MovementType = request.MovementType,
            Quantity = Math.Abs(request.QuantityDelta),
            Notes = request.Notes,
            CreatedBy = request.CreatedBy
        };
        context.StockMovements.Add(movement);
        await context.SaveChangesAsync(cancellationToken);

        if (request.QuantityDelta > 0)
            await publisher.Publish(new StockIncreasedEvent([request.VariantId]), cancellationToken);

        return Result.Success(movement.Id);
    }
}
