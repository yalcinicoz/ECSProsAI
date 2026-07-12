using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Inventory.Domain.Events;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Commands.AdjustStock;

public class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, Result<Guid>>
{
    private readonly IInventoryDbContext _context;
    private readonly IPublisher _publisher;

    public AdjustStockCommandHandler(IInventoryDbContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task<Result<Guid>> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        var warehouseExists = await _context.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, cancellationToken);
        if (!warehouseExists)
            return Result.Failure<Guid>("Depo bulunamadı.");

        var stock = await _context.Stocks.FirstOrDefaultAsync(
            s => s.VariantId == request.VariantId && s.WarehouseId == request.WarehouseId,
            cancellationToken);

        if (stock == null)
        {
            stock = new Stock
            {
                VariantId = request.VariantId,
                WarehouseId = request.WarehouseId,
                Quantity = 0,
                ReservedQuantity = 0
            };
            _context.Stocks.Add(stock);
        }

        var newQuantity = stock.Quantity + request.QuantityDelta;
        if (newQuantity < 0)
            return Result.Failure<Guid>("Stok miktarı negatife düşemez.");

        stock.Quantity = newQuantity;

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

        _context.StockMovements.Add(movement);
        await _context.SaveChangesAsync(cancellationToken);

        // H8: stok girişi — "stok gelince haber ver" tüketicisi dinler (kayıttan sonra).
        if (request.QuantityDelta > 0)
            await _publisher.Publish(new StockIncreasedEvent([request.VariantId]), cancellationToken);

        return Result.Success(movement.Id);
    }
}
