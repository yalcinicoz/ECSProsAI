using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Pos.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.EventHandlers;

public class PosSaleRefundedEventHandler : INotificationHandler<PosSaleRefundedEvent>
{
    private readonly IInventoryDbContext _context;

    public PosSaleRefundedEventHandler(IInventoryDbContext context)
    {
        _context = context;
    }

    public async Task Handle(PosSaleRefundedEvent notification, CancellationToken cancellationToken)
    {
        foreach (var item in notification.Items)
        {
            var quantity = (int)Math.Ceiling(item.Quantity);

            var stock = await _context.Stocks.FirstOrDefaultAsync(
                s => s.VariantId == item.VariantId && s.WarehouseId == notification.WarehouseId,
                cancellationToken);

            if (stock == null)
            {
                stock = new Stock
                {
                    VariantId = item.VariantId,
                    WarehouseId = notification.WarehouseId,
                    Quantity = quantity,
                    ReservedQuantity = 0
                };
                _context.Stocks.Add(stock);
            }
            else
            {
                stock.Quantity += quantity;
            }

            var movement = new StockMovement
            {
                VariantId = item.VariantId,
                FromWarehouseId = null,
                ToWarehouseId = notification.WarehouseId,
                MovementType = "pos_refund",
                Quantity = quantity,
                Notes = $"POS iade — {notification.SaleId}",
                CreatedBy = notification.RefundedBy
            };

            _context.StockMovements.Add(movement);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
