using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ECSPros.Api.Hubs;

/// <summary>
/// Yeni sipariş, sipariş durum değişikliği, stok uyarısı gibi genel bildirimleri dağıtır.
/// Admin kullanıcılar "orders" veya "stock" topic'lerine abone olabilir.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    /// <summary>Belirli bir topic'e abone ol (orders, stock, pos).</summary>
    public async Task Subscribe(string topic)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"topic:{topic}");
    }

    /// <summary>Topic aboneliğini iptal et.</summary>
    public async Task Unsubscribe(string topic)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"topic:{topic}");
    }

    public override async Task OnConnectedAsync()
    {
        // Kullanıcıya özgü grup — kişisel bildirimler için
        var userId = Context.UserIdentifier;
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        await base.OnConnectedAsync();
    }
}
