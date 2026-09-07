using ECSPros.Api.Services.Push;
using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.EventHandlers;

/// <summary>
/// Sipariş yaşam döngüsü push bildirimleri (docs/PUSH_BILDIRIM_ENTEGRASYONU.md §4.1, İ sınıfı): created (sepet siparişe
/// dönüştü), confirmed, shipped (kargo + takip no), delivered, cancelled (üyenin kendi iptali hariç). Hata push'ta kalır,
/// sipariş akışını asla düşürmez. Tekilleştirme order_status:{orderId}:{status}.
/// </summary>
public sealed class PushSiparisYardimcisi(IOrderDbContext odb, PushKuyruk kuyruk, ILogger<PushSiparisYardimcisi> logger)
{
    public async Task GonderAsync(Guid orderId, string type, string dedup, Func<Order.Domain.Entities.Order, Dictionary<string, string>>? ekVars, Func<Order.Domain.Entities.Order, bool>? kosul, CancellationToken ct)
    {
        try
        {
            var o = await odb.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId, ct);
            if (o is null || o.MemberId is null) return;
            if (kosul is not null && !kosul(o)) return;
            var vars = new Dictionary<string, string> { ["orderNumber"] = o.OrderNumber, ["orderId"] = o.Id.ToString() };
            if (ekVars is not null) foreach (var (k, v) in ekVars(o)) vars[k] = v;
            await kuyruk.EnqueueAsync(new PushIstek(type, o.MemberId, vars, dedup, o.FirmPlatformId), ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Sipariş push bildirimi kuyruğa alınamadı: {Type} {Order}", type, orderId); }
    }
}

public sealed class PushOrderCreatedHandler(PushSiparisYardimcisi y) : INotificationHandler<CartConvertedToOrderEvent>
{
    public Task Handle(CartConvertedToOrderEvent e, CancellationToken ct)
        => y.GonderAsync(e.OrderId, "order_created", $"order_created:{e.OrderId}", null, null, ct);
}
public sealed class PushOrderConfirmedHandler(PushSiparisYardimcisi y) : INotificationHandler<OrderConfirmedEvent>
{
    public Task Handle(OrderConfirmedEvent e, CancellationToken ct)
        => y.GonderAsync(e.OrderId, "order_confirmed", $"order_status:{e.OrderId}:confirmed", null, null, ct);
}
public sealed class PushOrderShippedHandler(PushSiparisYardimcisi y, IOrderDbContext odb) : INotificationHandler<OrderShippedEvent>
{
    public async Task Handle(OrderShippedEvent e, CancellationToken ct)
    {
        var s = await odb.Shipments.AsNoTracking().Where(x => x.OrderId == e.OrderId).OrderByDescending(x => x.CreatedAt).Select(x => new { x.CarrierName, x.TrackingNumber }).FirstOrDefaultAsync(ct);
        await y.GonderAsync(e.OrderId, "order_shipped", $"order_status:{e.OrderId}:shipped",
            o => new() { ["cargoName"] = s?.CarrierName ?? o.RequestedCargoName ?? "kargo", ["trackingNumber"] = s?.TrackingNumber ?? "" }, null, ct);
    }
}
public sealed class PushOrderDeliveredHandler(PushSiparisYardimcisi y) : INotificationHandler<OrderDeliveredEvent>
{
    public Task Handle(OrderDeliveredEvent e, CancellationToken ct)
        => y.GonderAsync(e.OrderId, "order_delivered", $"order_status:{e.OrderId}:delivered", null, null, ct);
}
public sealed class PushOrderCancelledHandler(PushSiparisYardimcisi y) : INotificationHandler<OrderCancelledEvent>
{
    // kullanıcı kendi iptalinde bildirim yok (CancelledBy = üye kimliği)
    public Task Handle(OrderCancelledEvent e, CancellationToken ct)
        => y.GonderAsync(e.OrderId, "order_cancelled", $"order_status:{e.OrderId}:cancelled", null, o => e.CancelledBy != o.MemberId, ct);
}
public sealed class PushReturnReceivedHandler(PushSiparisYardimcisi y) : INotificationHandler<ReturnReceivedEvent>
{
    public Task Handle(ReturnReceivedEvent e, CancellationToken ct)
        => y.GonderAsync(e.OrderId, "return_status", $"return_status:{e.ReturnId}:received", _ => new() { ["returnStatusLabel"] = "İadeniz teslim alındı" }, null, ct);
}
