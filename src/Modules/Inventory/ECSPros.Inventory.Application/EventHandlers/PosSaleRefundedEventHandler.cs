using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Inventory.Domain.Events;
using ECSPros.Pos.Domain.Events;
using MediatR;

namespace ECSPros.Inventory.Application.EventHandlers;

// Cutover (2026-07-14): POS iadesi mağazanın satılabilir rafına geri eklenir (StockOps.ReceiveAsync).
public class PosSaleRefundedEventHandler(IInventoryDbContext context, IPublisher publisher)
    : INotificationHandler<PosSaleRefundedEvent>
{
    public async Task Handle(PosSaleRefundedEvent notification, CancellationToken cancellationToken)
    {
        await StockTx.RunAsync(context, notification.Items.Select(i => i.VariantId), async () =>
        {
        foreach (var item in notification.Items)
        {
            var quantity = (int)Math.Ceiling(item.Quantity);
            await StockOps.ReceiveAsync(context, item.VariantId, notification.WarehouseId, quantity, preferReturns: false, cancellationToken);

            context.StockMovements.Add(new StockMovement
            {
                VariantId = item.VariantId,
                FromWarehouseId = null,
                ToWarehouseId = notification.WarehouseId,
                MovementType = "pos_refund",
                Quantity = quantity,
                Notes = $"POS iade — {notification.SaleId}",
                CreatedBy = notification.RefundedBy
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        if (notification.Items.Count > 0)
            await publisher.Publish(
                new StockIncreasedEvent(notification.Items.Select(i => i.VariantId).Distinct().ToList()),
                cancellationToken);
    }
}
