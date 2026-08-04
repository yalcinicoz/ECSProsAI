using ECSPros.Api.Services.Legacy;
using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.EventHandlers;

/// <summary>
/// F1 (2026-08-04, eski B4 doğrudan-INSERT geri-yazmasının yerine — kullanıcı kararı:
/// eski sistemin kendi SiparisOlusturFromModel servisi kullanılacak): sipariş ONAYLANDIĞINDA
/// eski sistem senkron KUYRUĞUNA yazar; gerçek gönderimi LegacySyncWorker dilimi yapar.
/// Kart (PayTR) siparişleri ödeme başarısında otomatik onaylandığından bu olayla kuyruğa
/// girer; kapıda ödemeli siparişler checkout anında girmiştir (unique index çift kaydı önler).
/// HATA-GÜVENLİ: kuyruğa yazım başarısız olsa bile onay akışı bozulmaz.
/// </summary>
public class OrderConfirmedLegacyQueueHandler(
    ILegacyOrderQueue queue,
    IOrderDbContext orderDb,
    ILogger<OrderConfirmedLegacyQueueHandler> logger) : INotificationHandler<OrderConfirmedEvent>
{
    public async Task Handle(OrderConfirmedEvent e, CancellationToken ct)
    {
        try
        {
            var order = await orderDb.Orders.AsNoTracking()
                .Where(o => o.Id == e.OrderId)
                .Select(o => new { o.FirmPlatformId, o.LegacyOrderId })
                .FirstOrDefaultAsync(ct);
            if (order is null || order.LegacyOrderId is not null) return; // yok/zaten eskide

            await queue.EnqueueAsync(e.OrderId, order.FirmPlatformId, "create", ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Legacy kuyruk handler hatası (orderId={OrderId})", e.OrderId);
        }
    }
}
