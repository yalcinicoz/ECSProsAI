using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ECSPros.Api.Hubs;

/// <summary>
/// Admin dashboard için canlı metrik hub'ı.
/// DashboardMetricsWorker her 30 saniyede bir MetricsUpdated event'i gönderir.
/// </summary>
[Authorize]
public class DashboardHub(DashboardPresenceTracker presence) : Hub
{
    // Sunucu → İstemci event'leri:
    // - MetricsUpdated(DashboardMetricsDto)   — periyodik tam tablo
    // - MetricChanged(key, value)             — tek metrik değişimi

    public override Task OnConnectedAsync()
    {
        presence.Increment();
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        presence.Decrement();
        return base.OnDisconnectedAsync(exception);
    }
}
