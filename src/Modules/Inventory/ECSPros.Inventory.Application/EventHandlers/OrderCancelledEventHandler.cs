using ECSPros.Inventory.Application.Services;
using ECSPros.Order.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.EventHandlers;

public class OrderCancelledEventHandler : INotificationHandler<OrderCancelledEvent>
{
    private readonly IInventoryDbContext _context;

    public OrderCancelledEventHandler(IInventoryDbContext context)
    {
        _context = context;
    }

    public async Task Handle(OrderCancelledEvent notification, CancellationToken cancellationToken)
    {
        var reservations = await _context.StockReservations
            .Where(r => r.ReferenceType == "order"
                     && r.ReferenceId == notification.OrderId
                     && r.Status == "reserved")
            .ToListAsync(cancellationToken);

        foreach (var reservation in reservations)
        {
            var stock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.Id == reservation.StockId, cancellationToken);

            if (stock != null)
                stock.ReservedQuantity = Math.Max(0, stock.ReservedQuantity - reservation.Quantity);

            reservation.Status = "cancelled";
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
