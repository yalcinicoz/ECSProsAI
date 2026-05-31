namespace ECSPros.Shared.Infrastructure.Messaging;

public interface IRealtimeNotificationService
{
    /// <summary>Tüm bağlı kullanıcılara sipariş bildirimi gönderir.</summary>
    Task SendOrderEventAsync(string eventType, object data, CancellationToken ct = default);

    /// <summary>Belirli fulfillment planına bağlı kullanıcılara bildirim gönderir.</summary>
    Task SendFulfillmentEventAsync(string planId, string eventType, object data, CancellationToken ct = default);

    /// <summary>Dashboard hub'ına metrik güncellemesi gönderir.</summary>
    Task SendDashboardMetricAsync(string metricKey, object value, CancellationToken ct = default);

    /// <summary>Belirli kullanıcıya kişisel bildirim gönderir.</summary>
    Task SendUserNotificationAsync(string userId, string eventType, object data, CancellationToken ct = default);
}
