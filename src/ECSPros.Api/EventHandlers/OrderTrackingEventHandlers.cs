using ECSPros.Api.Services.Tracking;
using ECSPros.Order.Domain.Events;
using ECSPros.Shared.Contracts.Tracking;
using MediatR;

namespace ECSPros.Api.EventHandlers;

/// <summary>
/// İE-2 Faz B-2: sipariş yaşam döngüsü → commerce event (outbox). Hata-güvenli: tracking
/// sipariş akışını asla bozmaz. purchaseAt=confirmed (varsayılan) ise order_completed burada;
/// purchaseAt=created ise checkout anında yazılmıştır (outbox dedup OrderId → çift olmaz).
/// </summary>
public class OrderConfirmedTrackingHandler(
    IOrderTrackingEventBuilder builder,
    ICommerceEventPublisher publisher,
    ILogger<OrderConfirmedTrackingHandler> logger) : INotificationHandler<OrderConfirmedEvent>
{
    public async Task Handle(OrderConfirmedEvent e, CancellationToken ct)
    {
        try
        {
            var ev = await builder.BuildOrderCompletedAsync(e.OrderId, ct);
            if (ev is null) return;
            await publisher.PublishAsync(ev, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Takip order_completed üretilemedi (orderId={OrderId})", e.OrderId);
        }
    }
}

/// <summary>Onaylanmış sipariş iptali → refund (tam). Onaylanmamış siparişte purchase gitmediğinden atlanır.</summary>
public class OrderCancelledTrackingHandler(
    IOrderTrackingEventBuilder builder,
    ICommerceEventPublisher publisher,
    ILogger<OrderCancelledTrackingHandler> logger) : INotificationHandler<OrderCancelledEvent>
{
    public async Task Handle(OrderCancelledEvent e, CancellationToken ct)
    {
        try
        {
            var ev = await builder.BuildRefundAsync(e.OrderId, null, "cancelled", ct);
            if (ev is null) return;
            await publisher.PublishAsync(ev, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Takip refund (iptal) üretilemedi (orderId={OrderId})", e.OrderId);
        }
    }
}

/// <summary>İade teslim alındı → refund (kısmi, iade kalemleri).</summary>
public class ReturnReceivedTrackingHandler(
    IOrderTrackingEventBuilder builder,
    ICommerceEventPublisher publisher,
    ILogger<ReturnReceivedTrackingHandler> logger) : INotificationHandler<ReturnReceivedEvent>
{
    public async Task Handle(ReturnReceivedEvent e, CancellationToken ct)
    {
        try
        {
            var kalemler = e.Items.Select(i => (i.VariantId, i.Quantity)).ToList();
            var ev = await builder.BuildRefundAsync(e.OrderId, kalemler, "return_received", ct);
            if (ev is null) return;
            await publisher.PublishAsync(ev, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Takip refund (iade) üretilemedi (orderId={OrderId})", e.OrderId);
        }
    }
}
