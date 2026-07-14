using ECSPros.Inventory.Application.Services;
using ECSPros.Order.Domain.Events;
using MediatR;

namespace ECSPros.Inventory.Application.EventHandlers;

// Cutover (2026-07-14): rezervasyon artık RAF seviyesinde (StockOps deponun raflarından greedy
// tahsis eder). Ship/Cancel handler'ları rezervasyonları StockId üzerinden işlediğinden değişmedi.
public class OrderConfirmedEventHandler(IInventoryDbContext context) : INotificationHandler<OrderConfirmedEvent>
{
    public async Task Handle(OrderConfirmedEvent notification, CancellationToken cancellationToken)
    {
        foreach (var item in notification.Items)
            await StockOps.ReserveAsync(context, item.VariantId, notification.WarehouseId,
                item.Quantity, "order", notification.OrderId, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }
}
