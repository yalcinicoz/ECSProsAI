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
        // WarehouseId boş (Guid.Empty) → online sipariş (ör. storefront ödeme onayı): depo-bağımsız
        // satılabilir-online raflar arası rezerve et. Dolu → belirli depo (admin onay akışı, değişmedi).
        var online = notification.WarehouseId == Guid.Empty;
        foreach (var item in notification.Items)
        {
            if (online)
                await StockOps.ReserveOnlineAsync(context, item.VariantId,
                    item.Quantity, "order", notification.OrderId, cancellationToken);
            else
                await StockOps.ReserveAsync(context, item.VariantId, notification.WarehouseId,
                    item.Quantity, "order", notification.OrderId, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
