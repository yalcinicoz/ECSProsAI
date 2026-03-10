using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Order.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.EventHandlers;

public class ReturnReceivedEventHandler : INotificationHandler<ReturnReceivedEvent>
{
    private readonly IInventoryDbContext _context;

    public ReturnReceivedEventHandler(IInventoryDbContext context)
    {
        _context = context;
    }

    public async Task Handle(ReturnReceivedEvent notification, CancellationToken cancellationToken)
    {
        foreach (var item in notification.Items)
        {
            var stock = await _context.Stocks.FirstOrDefaultAsync(
                s => s.VariantId == item.VariantId && s.WarehouseId == notification.WarehouseId,
                cancellationToken);

            if (stock is null)
            {
                stock = new Stock
                {
                    VariantId = item.VariantId,
                    WarehouseId = notification.WarehouseId,
                    Quantity = item.Quantity,
                    ReservedQuantity = 0
                };
                _context.Stocks.Add(stock);
            }
            else
            {
                stock.Quantity += item.Quantity;
            }

            _context.StockMovements.Add(new StockMovement
            {
                VariantId = item.VariantId,
                FromWarehouseId = null,
                ToWarehouseId = notification.WarehouseId,
                MovementType = "return",
                Quantity = item.Quantity,
                Notes = $"Sipariş iadesi — {notification.OrderId}",
                CreatedBy = notification.ReceivedBy
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
