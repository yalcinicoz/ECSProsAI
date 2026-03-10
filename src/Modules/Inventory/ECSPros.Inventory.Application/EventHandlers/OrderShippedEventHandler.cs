using ECSPros.Inventory.Application.Services;
using ECSPros.Order.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.EventHandlers;

public class OrderShippedEventHandler : INotificationHandler<OrderShippedEvent>
{
    private readonly IInventoryDbContext _context;

    public OrderShippedEventHandler(IInventoryDbContext context)
    {
        _context = context;
    }

    public async Task Handle(OrderShippedEvent notification, CancellationToken cancellationToken)
    {
        // Bu sipariş için tüm aktif rezervasyonları bul
        var reservations = await _context.StockReservations
            .Where(r => r.ReferenceType == "order"
                     && r.ReferenceId == notification.OrderId
                     && r.Status == "reserved")
            .ToListAsync(cancellationToken);

        foreach (var reservation in reservations)
        {
            var stock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.Id == reservation.StockId, cancellationToken);

            if (stock is not null)
            {
                // Rezervasyonu serbest bırak ve stoktan gerçekten düş
                stock.ReservedQuantity = Math.Max(0, stock.ReservedQuantity - reservation.Quantity);
                stock.Quantity = Math.Max(0, stock.Quantity - reservation.Quantity);
            }

            reservation.Status = "picked";
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
