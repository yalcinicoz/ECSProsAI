namespace ECSPros.Shared.Infrastructure.Messaging;

public interface IRealtimeNotificationService
{
    /// <summary>
    /// Sipariş bildirimi. Y3 (K2): bildirim YALNIZ o kanalı görebilen abonelere gider —
    /// aksi hâlde liste filtrelense bile "başka kanalda sipariş oluştu" bilgisi sızar.
    /// <paramref name="firmPlatformId"/> null ise yalnız kanal kısıtı olmayan aboneler alır.
    /// </summary>
    Task SendOrderEventAsync(string eventType, object data, Guid? firmPlatformId, CancellationToken ct = default);

    /// <summary>Belirli fulfillment planına bağlı kullanıcılara bildirim gönderir.</summary>
    Task SendFulfillmentEventAsync(string planId, string eventType, object data, CancellationToken ct = default);

    /// <summary>Dashboard hub'ına metrik güncellemesi gönderir.</summary>
    Task SendDashboardMetricAsync(string metricKey, object value, CancellationToken ct = default);

    /// <summary>Belirli kullanıcıya kişisel bildirim gönderir.</summary>
    Task SendUserNotificationAsync(string userId, string eventType, object data, CancellationToken ct = default);

    /// <summary>Ürün soruları topic'ine (panel moderasyonu) bildirim gönderir.</summary>
    Task SendQuestionEventAsync(string eventType, object data, Guid? firmPlatformId, CancellationToken ct = default);
}
