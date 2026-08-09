using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Order.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.EventHandlers;

/// <summary>OP4: masa ilerlemesi — OrderItem.FinalSortQuantity/FinalScan* ve
/// Order.PackingStationCode/PackingSlotNumber senkron güncellenir.</summary>
public class DeskLineProgressEventHandler(IOrderDbContext db)
    : INotificationHandler<DeskLineProgressEvent>
{
    public async Task Handle(DeskLineProgressEvent notification, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var item = await db.OrderItems.FirstOrDefaultAsync(i => i.Id == notification.OrderItemId, ct);
        if (item is not null)
        {
            item.FinalSortQuantity += notification.FinalSortDelta;
            if (notification.FinalScanDelta > 0)
            {
                item.FinalScanQuantity += notification.FinalScanDelta;
                item.FinalScanBy = notification.ActorId;
                item.FinalScanAt = now;
            }
        }

        if (notification.StationCode is not null || notification.SlotNumber is not null)
        {
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == notification.OrderId, ct);
            if (order is not null)
            {
                if (notification.StationCode is not null) order.PackingStationCode = notification.StationCode;
                if (notification.SlotNumber is not null) order.PackingSlotNumber = notification.SlotNumber;
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
