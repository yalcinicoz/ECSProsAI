using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Order.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.EventHandlers;

/// <summary>OP3: ara ayrıştırma okutması — OrderItem.SortingBinQuantity ve
/// Order.SortingBinId (koli eşleme kaydı) senkron güncellenir.</summary>
public class PickingLinesSortedEventHandler(IOrderDbContext db)
    : INotificationHandler<PickingLinesSortedEvent>
{
    public async Task Handle(PickingLinesSortedEvent notification, CancellationToken ct)
    {
        var item = await db.OrderItems.FirstOrDefaultAsync(i => i.Id == notification.OrderItemId, ct);
        if (item is not null)
            item.SortingBinQuantity += notification.Quantity;

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == notification.OrderId, ct);
        if (order is not null && order.SortingBinId != notification.SortingBinId)
            order.SortingBinId = notification.SortingBinId;

        await db.SaveChangesAsync(ct);
    }
}
