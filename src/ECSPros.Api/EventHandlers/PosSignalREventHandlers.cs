using ECSPros.Pos.Domain.Events;
using ECSPros.Shared.Infrastructure.Messaging;
using MediatR;

namespace ECSPros.Api.EventHandlers;

/// <summary>
/// POS domain event'lerini dinleyip Dashboard ve Notification hub'larına iletir.
/// </summary>
public class PosSaleCompletedSignalRHandler(IRealtimeNotificationService notifier)
    : INotificationHandler<PosSaleCompletedEvent>
{
    public async Task Handle(PosSaleCompletedEvent notification, CancellationToken ct)
    {
        await notifier.SendDashboardMetricAsync("pos_sale", new
        {
            saleId = notification.SaleId,
            warehouseId = notification.WarehouseId,
            itemCount = notification.Items.Count,
            occurredAt = notification.OccurredAt
        }, ct);

        await notifier.SendOrderEventAsync("PosSaleCompleted", new
        {
            saleId = notification.SaleId,
            occurredAt = notification.OccurredAt
        }, ct);
    }
}
