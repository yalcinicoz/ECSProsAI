using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Inventory.Domain.Events;
using ECSPros.Order.Domain.Events;
using MediatR;

namespace ECSPros.Inventory.Application.EventHandlers;

// Cutover (2026-07-14): müşteri iadesi satışa-KAPALI kısma (İade/Defo) alınır — muayene sonrası
// satışa açık rafa transfer edilir (StockOps.ReceiveAsync preferReturns:true).
public class ReturnReceivedEventHandler(IInventoryDbContext context, IPublisher publisher)
    : INotificationHandler<ReturnReceivedEvent>
{
    public async Task Handle(ReturnReceivedEvent notification, CancellationToken cancellationToken)
    {
        foreach (var item in notification.Items)
        {
            await StockOps.ReceiveAsync(context, item.VariantId, notification.WarehouseId, item.Quantity, preferReturns: true, cancellationToken);

            context.StockMovements.Add(new StockMovement
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

        await context.SaveChangesAsync(cancellationToken);

        if (notification.Items.Count > 0)
            await publisher.Publish(
                new StockIncreasedEvent(notification.Items.Select(i => i.VariantId).Distinct().ToList()),
                cancellationToken);
    }
}
