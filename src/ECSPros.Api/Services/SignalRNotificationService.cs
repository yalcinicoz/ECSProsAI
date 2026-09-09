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
    /// <summary>
    /// Y3 (K2): sipariş olayı kanal grubuna gider. Aboneler <see cref="NotificationHub.Subscribe"/>
    /// sırasında YALNIZ görebildikleri kanalların gruplarına alınır; kısıtsız kullanıcılar
    /// (süper admin / kanal kapsamsız yetki) ayrıca "topic:orders:all" grubundadır.
    /// Kanal bilinmiyorsa (null) bildirim yalnız kısıtsız abonelere gider — sızıntı yerine eksik bildirim.
    /// </summary>
    public async Task SendOrderEventAsync(string eventType, object data, Guid? firmPlatformId, CancellationToken ct = default)
    {
        await notificationHub.Clients.Group(NotificationHub.TumKanallarGrubu("orders"))
            .SendAsync(eventType, data, ct);
        await notificationHub.Clients.Group("topic:orders")   // eski genel grup (kısıtsız aboneler)
            .SendAsync(eventType, data, ct);

        if (firmPlatformId is { } kanal)
            await notificationHub.Clients.Group(NotificationHub.KanalGrubu("orders", kanal))
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

    /// <summary>
    /// Ürün sorusu bildirimi. Y3 (K2): moderasyon yetkisi kanal kapsamlıdır — bildirim yalnız
    /// o kanalı görebilen abonelere gider. Kanal bilinmiyorsa yalnız kısıtsız aboneler alır.
    /// </summary>
    public async Task SendQuestionEventAsync(string eventType, object data, Guid? firmPlatformId, CancellationToken ct = default)
    {
        await notificationHub.Clients.Group(NotificationHub.TumKanallarGrubu("questions"))
            .SendAsync(eventType, data, ct);
        await notificationHub.Clients.Group("topic:questions")   // eski genel grup (kısıtsız aboneler)
            .SendAsync(eventType, data, ct);

        if (firmPlatformId is { } kanal)
            await notificationHub.Clients.Group(NotificationHub.KanalGrubu("questions", kanal))
                .SendAsync(eventType, data, ct);
    }
}
