using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Order.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.EventHandlers;

/// <summary>
/// OP1: toplama satırları personele dağıtıldığında OrderItem.PickAssignedTo/At
/// alanlarını senkron günceller (kalem izlenebilirliği sipariş tarafında da görünür).
/// </summary>
public class PickingLinesAssignedEventHandler(IOrderDbContext db)
    : INotificationHandler<PickingLinesAssignedEvent>
{
    public async Task Handle(PickingLinesAssignedEvent notification, CancellationToken ct)
    {
        var itemIds = notification.Lines.Select(l => l.OrderItemId).ToList();
        var items = await db.OrderItems
            .Where(i => itemIds.Contains(i.Id))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.PickAssignedTo = notification.AssignedTo;
            item.PickAssignedAt = now;
        }
        await db.SaveChangesAsync(ct);
    }
}
