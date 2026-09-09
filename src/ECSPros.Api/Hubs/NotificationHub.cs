using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ECSPros.Api.Hubs;

/// <summary>
/// Panel bildirimleri (sipariş, stok, POS, soru…).
///
/// Y3 3. tur (2026-09-09, karar K2): abonelik ARTIK YETKİYE ve KANAL KAPSAMINA tabidir.
/// Önceden yetkili her kullanıcı "topic:orders" grubuna girip TÜM kanalların sipariş olaylarını
/// alıyordu; liste filtrelense bile "başka kanalda sipariş oluştu" bilgisi sızıyordu.
///
/// Kural: <see cref="Subscribe"/> topic için gerekli yetkiyi arar; kullanıcı kanal kapsamlıysa
/// yalnız görebildiği kanalların gruplarına alınır, kısıtsızsa (süper admin / kanal kapsamsız
/// yetki) "tüm kanallar" grubuna alınır.
/// </summary>
[Authorize]
public class NotificationHub(
    IEtkinYetkiServisi yetkiServisi,
    ECSPros.Iam.Application.Yetkilendirme.IYetkisizErisimKaydedici erisimKaydi) : Hub
{
    /// <summary>Kanal kısıtı olmayan abonelerin grubu (süper admin dahil) — topic başına.</summary>
    public static string TumKanallarGrubu(string topic) => $"topic:{topic}:all";

    public static string KanalGrubu(string topic, Guid kanalId) => $"topic:{topic}:{kanalId:N}";

    /// <summary>Topic → gerekli yetki. Listede olmayan topic'e abone olunamaz.</summary>
    private static readonly Dictionary<string, string> TopicYetkileri = new(StringComparer.OrdinalIgnoreCase)
    {
        ["orders"] = Permissions.OrdersView,
        ["stock"] = Permissions.InventoryView,
        ["pos"] = Permissions.PosView,
        ["fulfillment"] = Permissions.FulfillmentView,
        ["questions"] = Permissions.StorefrontModerationView,
    };

    /// <summary>Ret kaydı isteği bozmaz: yazılamazsa abonelik yine reddedilir.</summary>
    private async Task KaydetAsync(Guid userId, string yetki, string topic)
    {
        try
        {
            await erisimKaydi.DeneAsync(new ECSPros.Iam.Application.Yetkilendirme.YetkisizErisimKaydi(
                "bildirim", yetki, $"hub:{topic}", "SUBSCRIBE", 0, 0), userId, Context.ConnectionAborted);
        }
        catch { /* denetim kaydı reddi engellemez */ }
    }

    /// <summary>Belirli bir topic'e abone ol — yetki ve kanal kapsamı denetlenir.</summary>
    public async Task Subscribe(string topic)
    {
        if (!TopicYetkileri.TryGetValue(topic ?? "", out var gerekliYetki))
            throw new HubException("Bilinmeyen bildirim konusu.");

        var superAdmin = Context.User?.FindFirst("sa")?.Value == "true";
        var sub = Context.User?.FindFirst("sub")?.Value;

        if (!superAdmin)
        {
            if (!Guid.TryParse(sub, out var userId))
                throw new HubException("Bu bildirimlere abone olma yetkiniz yok.");

            var yetkiler = await yetkiServisi.GetirAsync(userId, Context.ConnectionAborted);
            if (!yetkiler.Var(gerekliYetki))
            {
                // Y8 (§J.1): hub aboneliği reddi de bir yetkisiz erişim denemesidir (örneklenerek yazılır).
                await KaydetAsync(userId, gerekliYetki, topic!);
                throw new HubException("Bu bildirimlere abone olma yetkiniz yok.");
            }

            var kanallar = yetkiler.Kanallar(gerekliYetki);
            if (kanallar is not null)
            {
                // Kanal kapsamlı kullanıcı: YALNIZ görebildiği kanalların grupları.
                foreach (var kanal in kanallar)
                    await Groups.AddToGroupAsync(Context.ConnectionId, KanalGrubu(topic, kanal));
                return;
            }
        }

        // Kısıtsız (süper admin ya da kanal kapsamsız yetki): tüm kanallar + eski genel grup.
        await Groups.AddToGroupAsync(Context.ConnectionId, TumKanallarGrubu(topic!));
        await Groups.AddToGroupAsync(Context.ConnectionId, $"topic:{topic}");
    }

    /// <summary>Topic aboneliğini iptal et (kanal grupları dahil).</summary>
    public async Task Unsubscribe(string topic)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"topic:{topic}");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TumKanallarGrubu(topic ?? ""));

        var sub = Context.User?.FindFirst("sub")?.Value;
        if (Guid.TryParse(sub, out var userId) && TopicYetkileri.TryGetValue(topic ?? "", out var yetki))
        {
            var yetkiler = await yetkiServisi.GetirAsync(userId, Context.ConnectionAborted);
            foreach (var kanal in yetkiler.Kanallar(yetki) ?? (IReadOnlySet<Guid>)new HashSet<Guid>())
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, KanalGrubu(topic!, kanal));
        }
    }

    public override async Task OnConnectedAsync()
    {
        // Kullanıcıya özgü grup — kişisel bildirimler (kapsam gerektirmez, hedef zaten kullanıcıdır)
        var userId = Context.UserIdentifier;
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        await base.OnConnectedAsync();
    }
}
