using Microsoft.EntityFrameworkCore;
using ECSPros.Order.Domain.Events;
using ECSPros.Shared.Infrastructure.Messaging;
using MediatR;

namespace ECSPros.Api.EventHandlers;

/// <summary>
/// Order domain event'lerini dinleyip NotificationHub'a iletir.
/// </summary>
public class OrderConfirmedSignalRHandler(IRealtimeNotificationService notifier, ECSPros.Order.Application.Services.IOrderDbContext db)
    : INotificationHandler<OrderConfirmedEvent>
{
    public async Task Handle(OrderConfirmedEvent notification, CancellationToken ct)
    {
        await notifier.SendOrderEventAsync("OrderConfirmed", new
        {
            orderId = notification.OrderId,
            confirmedBy = notification.ConfirmedBy,
            itemCount = notification.Items.Count,
            occurredAt = notification.OccurredAt
        }, await SiparisKanali.BulAsync(db, notification.OrderId, ct), ct);
    }
}

public class OrderShippedSignalRHandler(IRealtimeNotificationService notifier, ECSPros.Order.Application.Services.IOrderDbContext db)
    : INotificationHandler<OrderShippedEvent>
{
    public async Task Handle(OrderShippedEvent notification, CancellationToken ct)
    {
        await notifier.SendOrderEventAsync("OrderShipped", new
        {
            orderId = notification.OrderId,
            shippedBy = notification.ShippedBy,
            itemCount = notification.Items.Count,
            occurredAt = notification.OccurredAt
        }, await SiparisKanali.BulAsync(db, notification.OrderId, ct), ct);
    }
}

public class OrderCancelledSignalRHandler(IRealtimeNotificationService notifier, ECSPros.Order.Application.Services.IOrderDbContext db)
    : INotificationHandler<OrderCancelledEvent>
{
    public async Task Handle(OrderCancelledEvent notification, CancellationToken ct)
    {
        await notifier.SendOrderEventAsync("OrderCancelled", new
        {
            orderId = notification.OrderId,
            cancelledBy = notification.CancelledBy,
            occurredAt = notification.OccurredAt
        }, await SiparisKanali.BulAsync(db, notification.OrderId, ct), ct);
    }
}

/// <summary>
/// Y3 (K2): sipariş olayının KANALI — bildirim yalnız o kanalı görebilen abonelere gitsin diye.
/// Domain olayları kanal taşımadığından tek kayıtlık okuma yapılır (olaylar seyrek: onay/kargo/iptal).
/// </summary>
internal static class SiparisKanali
{
    public static async Task<Guid?> BulAsync(ECSPros.Order.Application.Services.IOrderDbContext db, Guid orderId, CancellationToken ct)
        => await db.Orders.AsNoTracking().Where(o => o.Id == orderId).Select(o => (Guid?)o.FirmPlatformId).FirstOrDefaultAsync(ct);
}
