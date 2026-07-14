using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Pos.Domain.Events;
using MediatR;

namespace ECSPros.Inventory.Application.EventHandlers;

// Cutover (2026-07-14): stok deponun raflarından greedy düşülür (StockOps.ConsumeAsync).
public class PosSaleCompletedEventHandler(IInventoryDbContext context) : INotificationHandler<PosSaleCompletedEvent>
{
    public async Task Handle(PosSaleCompletedEvent notification, CancellationToken cancellationToken)
    {
        foreach (var soldItem in notification.Items)
        {
            var quantity = (int)Math.Ceiling(soldItem.Quantity);
            await StockOps.ConsumeAsync(context, soldItem.VariantId, notification.WarehouseId, quantity, cancellationToken);

            context.StockMovements.Add(new StockMovement
            {
                VariantId = soldItem.VariantId,
                FromWarehouseId = notification.WarehouseId,
                ToWarehouseId = null,
                MovementType = "pos_sale",
                Quantity = quantity,
                Notes = $"POS satışı — {notification.SaleId}",
                CreatedBy = notification.CompletedBy
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
