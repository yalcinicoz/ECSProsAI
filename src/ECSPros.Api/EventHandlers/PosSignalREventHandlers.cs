using Microsoft.EntityFrameworkCore;
using ECSPros.Pos.Domain.Events;
using ECSPros.Shared.Infrastructure.Messaging;
using MediatR;

namespace ECSPros.Api.EventHandlers;

/// <summary>
/// POS domain event'lerini dinleyip Dashboard ve Notification hub'larına iletir.
/// </summary>
public class PosSaleCompletedSignalRHandler(IRealtimeNotificationService notifier, ECSPros.Pos.Application.Services.IPosDbContext db)
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

        // Y3 (K2): POS satışının kanalı KASADAN gelir; bildirim yalnız o kanalı görebilenlere gider.
        var kanal = await db.PosSales.AsNoTracking().Where(x => x.Id == notification.SaleId)
            .Select(x => (Guid?)x.Register.FirmPlatformId).FirstOrDefaultAsync(ct);

        await notifier.SendOrderEventAsync("PosSaleCompleted", new
        {
            saleId = notification.SaleId,
            occurredAt = notification.OccurredAt
        }, kanal, ct);
    }
}
