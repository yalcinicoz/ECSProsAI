using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Shared.Infrastructure.Messaging;
using MediatR;

namespace ECSPros.Api.EventHandlers;

/// <summary>
/// Fulfillment domain event'lerini dinleyip FulfillmentHub'a iletir.
/// </summary>
public class PickingPlanCreatedSignalRHandler(IRealtimeNotificationService notifier)
    : INotificationHandler<PickingPlanCreatedEvent>
{
    public async Task Handle(PickingPlanCreatedEvent notification, CancellationToken ct)
    {
        await notifier.SendFulfillmentEventAsync(notification.PlanId.ToString(), "PickingPlanCreated", new
        {
            planId = notification.PlanId,
            warehouseId = notification.WarehouseId,
            createdBy = notification.CreatedBy,
            orderCount = notification.Orders.Count,
            occurredAt = notification.OccurredAt
        }, ct);
    }
}

public class PickingPlanCompletedSignalRHandler(IRealtimeNotificationService notifier)
    : INotificationHandler<PickingPlanCompletedEvent>
{
    public async Task Handle(PickingPlanCompletedEvent notification, CancellationToken ct)
    {
        await notifier.SendFulfillmentEventAsync(notification.PlanId.ToString(), "PickingPlanCompleted", new
        {
            planId = notification.PlanId,
            completedBy = notification.CompletedBy,
            orderCount = notification.OrderIds.Count,
            occurredAt = notification.OccurredAt
        }, ct);
    }
}
