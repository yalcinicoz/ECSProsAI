using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ECSPros.Api.Hubs;

/// <summary>
/// Depo operasyonları için real-time hub.
/// Picking plan çalışanları belirli plan grubuna katılır;
/// barkod tarama, bin durum değişikliği, plan tamamlanma bildirimleri alır.
/// </summary>
[Authorize]
public class FulfillmentHub : Hub
{
    /// <summary>Belirli picking plan'ının güncellemelerini almak için gruba katıl.</summary>
    public async Task JoinPlan(string planId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"plan:{planId}");
    }

    /// <summary>Picking plan grubundan ayrıl.</summary>
    public async Task LeavePlan(string planId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"plan:{planId}");
    }

    /// <summary>Belirli depoya ait tüm güncellemeleri almak için gruba katıl.</summary>
    public async Task JoinWarehouse(string warehouseId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"warehouse:{warehouseId}");
    }

    public async Task LeaveWarehouse(string warehouseId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"warehouse:{warehouseId}");
    }
}
