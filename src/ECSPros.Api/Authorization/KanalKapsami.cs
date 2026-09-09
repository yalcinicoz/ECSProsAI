using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Authorization;

/// <summary>
/// Y3 (2026-09-09, karar K1+K2): isteği yapan kullanıcının bir yetki için görebileceği KANALLAR.
///
/// Dönüş <c>null</c> ise kısıt yoktur: süper admin, ya da yetki kanal kapsamlı değildir
/// (kanaldan bağımsız liste). Boş küme dönerse kullanıcı o listede HİÇBİR satır görmemelidir
/// (default deny) — çağıranlar bunu <see cref="ECSPros.Shared.Kernel.Grid.GridRequest.KanalKisiti"/>
/// olarak taşır, grid çekirdeği sorguya uygular.
///
/// Kapsam yalnız "yapabilir mi"yi değil, HANGİ SATIRLARI GÖRÜR'ü belirler: liste, arama, sayaç,
/// dashboard, rapor, export ve toplu işlem aynı kısıttan geçer.
/// </summary>
public interface IKanalKapsami
{
    Task<IReadOnlyCollection<Guid>?> KanallarAsync(string permissionKey, CancellationToken ct = default);

    /// <summary>
    /// Y3 3. tur: KANAL PARAMETRELİ ekranlar için (vitrin, menü, kanal ürünleri, pazaryeri…).
    /// Bu uçlar liste filtrelemez, tek bir kanalla çalışır — sorulacak şey "bu kanala erişebiliyor mu".
    /// false dönerse çağıran <b>404</b> döndürür (kaydın/kanalın varlığı sızmasın, tasarım §C.2).
    /// </summary>
    Task<bool> ErisebilirMiAsync(string permissionKey, Guid kanalId, CancellationToken ct = default);
}

public sealed class KanalKapsami(IHttpContextAccessor http, IEtkinYetkiServisi yetkiServisi) : IKanalKapsami
{
    public async Task<IReadOnlyCollection<Guid>?> KanallarAsync(string permissionKey, CancellationToken ct = default)
    {
        var user = http.HttpContext?.User;
        if (user is null) return Array.Empty<Guid>();                 // bağlam yoksa hiçbir şey

        if (user.FindFirst("sa")?.Value == "true") return null;       // süper admin: sınırsız (K5)

        // Yetki kanal kapsamlı değilse kısıt uygulanmaz (panelde kanal seçici de görünmez).
        var tanim = PermissionKatalogu.Bul(permissionKey);
        if (tanim is { KanalKapsamli: false }) return null;

        var sub = user.FindFirst("sub")?.Value
                  ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId)) return Array.Empty<Guid>();

        var yetkiler = await yetkiServisi.GetirAsync(userId, ct);
        if (yetkiler.SuperAdmin) return null;

        // Yetki yoksa uç zaten 403 döner; yine de kapsam boş verilir (savunmalı).
        return yetkiler.Kanallar(permissionKey)?.ToList() ?? (IReadOnlyCollection<Guid>)Array.Empty<Guid>();
    }

    public async Task<bool> ErisebilirMiAsync(string permissionKey, Guid kanalId, CancellationToken ct = default)
    {
        var kanallar = await KanallarAsync(permissionKey, ct);
        if (kanallar is null) return true;             // kısıt yok (süper admin / kapsamsız yetki)
        return kanalId != Guid.Empty && kanallar.Contains(kanalId);
    }
}
