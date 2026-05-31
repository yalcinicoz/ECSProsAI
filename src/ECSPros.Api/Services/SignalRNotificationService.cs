using ECSPros.Api.Hubs;
using ECSPros.Shared.Infrastructure.Messaging;
using Microsoft.AspNetCore.SignalR;

namespace ECSPros.Api.Services;

/// <summary>
/// IRealtimeNotificationService'in SignalR implementasyonu.
/// Modüllerdeki event handler'lar bu servisi inject ederek hub'lara mesaj gönderir.
/// </summary>
public class SignalRNotificationService(
    IHubContext<NotificationHub> notificationHub,
    IHubContext<FulfillmentHub> fulfillmentHub,
    IHubContext<DashboardHub> dashboardHub) : IRealtimeNotificationService
{
    public async Task SendOrderEventAsync(string eventType, object data, CancellationToken ct = default)
    {
        await notificationHub.Clients.Group("topic:orders")
            .SendAsync(eventType, data, ct);
    }

    public async Task SendFulfillmentEventAsync(string planId, string eventType, object data, CancellationToken ct = default)
    {
        await fulfillmentHub.Clients.Group($"plan:{planId}")
            .SendAsync(eventType, data, ct);

        // Ayrıca genel fulfillment topic'ine de gönder
        await notificationHub.Clients.Group("topic:fulfillment")
            .SendAsync(eventType, data, ct);
    }

    public async Task SendDashboardMetricAsync(string metricKey, object value, CancellationToken ct = default)
    {
        await dashboardHub.Clients.All
            .SendAsync("MetricChanged", new { key = metricKey, value }, ct);
    }

    public async Task SendUserNotificationAsync(string userId, string eventType, object data, CancellationToken ct = default)
    {
        await notificationHub.Clients.Group($"user:{userId}")
            .SendAsync(eventType, data, ct);
    }
}
