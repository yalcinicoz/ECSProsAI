using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Order.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.EventHandlers;

/// <summary>
/// OP2: toplama okutması — OrderItem.PickedBy/At (ve tek ürünlü hatta FinalScan*)
/// alanlarını günceller; kalem izlenebilirliği sipariş tarafında da tam olur.
/// </summary>
public class PickingLinePickedEventHandler(IOrderDbContext db)
    : INotificationHandler<PickingLinePickedEvent>
{
    public async Task Handle(PickingLinePickedEvent notification, CancellationToken ct)
    {
        var itemIds = notification.Items.Select(i => i.OrderItemId).Distinct().ToList();
        var items = await db.OrderItems.Where(i => itemIds.Contains(i.Id)).ToListAsync(ct);
        var byId = items.ToDictionary(i => i.Id);
        var now = DateTime.UtcNow;

        foreach (var picked in notification.Items)
        {
            if (!byId.TryGetValue(picked.OrderItemId, out var item)) continue;
            item.PickedBy = notification.ActorId;
            item.PickedAt = now;
            if (picked.FinalScanned)
            {
                item.FinalSortQuantity = picked.Quantity;
                item.FinalScanBy = notification.ActorId;
                item.FinalScanAt = now;
                item.FinalScanQuantity = picked.Quantity;
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
