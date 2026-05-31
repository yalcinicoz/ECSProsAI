using ECSPros.Order.Domain.Events;
using ECSPros.Shared.Infrastructure.Messaging;
using MediatR;

namespace ECSPros.Api.EventHandlers;

/// <summary>
/// Order domain event'lerini dinleyip NotificationHub'a iletir.
/// </summary>
public class OrderConfirmedSignalRHandler(IRealtimeNotificationService notifier)
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
        }, ct);
    }
}

public class OrderShippedSignalRHandler(IRealtimeNotificationService notifier)
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
        }, ct);
    }
}

public class OrderCancelledSignalRHandler(IRealtimeNotificationService notifier)
    : INotificationHandler<OrderCancelledEvent>
{
    public async Task Handle(OrderCancelledEvent notification, CancellationToken ct)
    {
        await notifier.SendOrderEventAsync("OrderCancelled", new
        {
            orderId = notification.OrderId,
            cancelledBy = notification.CancelledBy,
            occurredAt = notification.OccurredAt
        }, ct);
    }
}
