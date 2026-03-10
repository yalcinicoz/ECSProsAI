using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Order.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.EventHandlers;

public class OrderConfirmedEventHandler : INotificationHandler<OrderConfirmedEvent>
{
    private readonly IInventoryDbContext _context;

    public OrderConfirmedEventHandler(IInventoryDbContext context)
    {
        _context = context;
    }

    public async Task Handle(OrderConfirmedEvent notification, CancellationToken cancellationToken)
    {
        foreach (var item in notification.Items)
        {
            var stock = await _context.Stocks.FirstOrDefaultAsync(
                s => s.VariantId == item.VariantId && s.WarehouseId == notification.WarehouseId,
                cancellationToken);

            if (stock == null)
            {
                stock = new Stock
                {
                    VariantId = item.VariantId,
                    WarehouseId = notification.WarehouseId,
                    Quantity = 0,
                    ReservedQuantity = 0
                };
                _context.Stocks.Add(stock);
                await _context.SaveChangesAsync(cancellationToken);
            }

            stock.ReservedQuantity += item.Quantity;

            var reservation = new StockReservation
            {
                StockId = stock.Id,
                VariantId = item.VariantId,
                WarehouseId = notification.WarehouseId,
                Quantity = item.Quantity,
                ReferenceType = "order",
                ReferenceId = notification.OrderId,
                Status = "reserved"
            };

            _context.StockReservations.Add(reservation);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
