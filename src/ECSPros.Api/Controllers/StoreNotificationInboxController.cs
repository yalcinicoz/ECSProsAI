using ECSPros.Api.Services.Push;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// Uygulama içi "Bildirimlerim" (docs/BILDIRIMLERIM_BACKEND_ISTEGI.md, 2026-09-08). Hepsi ÜYE JWT ister; cihaz/web token'ı ya da
/// kimliksiz istek 401 (MemberOnly politikası 403 verirdi; belge 401 istiyor → politika yerine elle denetim). firmPlatformId
/// isteğe bağlı (query ya da X-Firm-Platform); verilmezse üyenin tüm platformlardaki satırları. Zarf: {success, data, error, code}.
/// </summary>
[ApiController]
[Route("api/store/account/notifications")]
[AllowAnonymous]
public class StoreNotificationInboxController(BildirimKutusu kutu) : ControllerBase
{
    Guid? Uye()
    {
        if (User.Identity?.IsAuthenticated != true) return null;
        if (User.FindFirst("type")?.Value != "member") return null;
        return Guid.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var g) ? g : null;
    }
    IActionResult Yetkisiz() => Unauthorized(new { success = false, error = "Üye girişi gerekli.", code = "member_required" });
    IActionResult Yok() => NotFound(new { success = false, error = "Bildirim bulunamadı.", code = "not_found" });
    Guid? Platform([FromQuery] Guid? firmPlatformId) => firmPlatformId is { } g && g != Guid.Empty ? g : null;

    /// <summary>Liste (yeni→eski, sayfalı; pageSize ≤ 50). Her yanıtta tüm liste için unreadCount.</summary>
    [HttpGet]
    public async Task<IActionResult> Liste([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? firmPlatformId = null, CancellationToken ct = default)
    {
        if (Uye() is not { } uye) return Yetkisiz();
        var s = await kutu.ListeAsync(uye, Platform(firmPlatformId), page, pageSize, ct);
        return Ok(new { success = true, data = new
        {
            items = s.Items.Select(n => new { id = n.Id, dedupId = n.DedupId, type = n.Type, @class = n.Class, title = n.Title, body = n.Body, link = n.Link,
                imageUrl = n.ImageUrl, icon = n.Icon, createdAt = n.CreatedAt, readAt = n.ReadAt, dismissOnOpen = n.DismissOnOpen }),
            unreadCount = s.UnreadCount, totalCount = s.TotalCount, page = s.Page, pageSize = s.PageSize, totalPages = s.TotalPages, hasNextPage = s.HasNextPage,
        } });
    }

    /// <summary>Yalnız rozet için hafif uç.</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> Okunmamis([FromQuery] Guid? firmPlatformId = null, CancellationToken ct = default)
    {
        if (Uye() is not { } uye) return Yetkisiz();
        return Ok(new { success = true, data = new { unreadCount = await kutu.OkunmamisAsync(uye, Platform(firmPlatformId), ct) } });
    }

    /// <summary>Okundu (idempotent); şablon dismissOnOpen ise listeden de düşer. Yok/başkasının → 404.</summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> Okundu(Guid id, [FromQuery] Guid? firmPlatformId = null, CancellationToken ct = default)
    {
        if (Uye() is not { } uye) return Yetkisiz();
        if (!await kutu.OkunduAsync(uye, id, ct)) return Yok();
        return Ok(new { success = true, data = new { unreadCount = await kutu.OkunmamisAsync(uye, Platform(firmPlatformId), ct) } });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> TumunuOkundu([FromQuery] Guid? firmPlatformId = null, CancellationToken ct = default)
    {
        if (Uye() is not { } uye) return Yetkisiz();
        await kutu.TumunuOkunduAsync(uye, Platform(firmPlatformId), ct);
        return Ok(new { success = true, data = new { unreadCount = await kutu.OkunmamisAsync(uye, Platform(firmPlatformId), ct) } });
    }

    /// <summary>Sil (geri alma yok; zaten silinmiş → 200). Yok/başkasının → 404.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Sil(Guid id, [FromQuery] Guid? firmPlatformId = null, CancellationToken ct = default)
    {
        if (Uye() is not { } uye) return Yetkisiz();
        if (!await kutu.SilAsync(uye, id, ct)) return Yok();
        return Ok(new { success = true, data = new { unreadCount = await kutu.OkunmamisAsync(uye, Platform(firmPlatformId), ct) } });
    }

    [HttpDelete]
    public async Task<IActionResult> TumunuSil([FromQuery] Guid? firmPlatformId = null, CancellationToken ct = default)
    {
        if (Uye() is not { } uye) return Yetkisiz();
        await kutu.TumunuSilAsync(uye, Platform(firmPlatformId), ct);
        return Ok(new { success = true, data = new { unreadCount = 0 } });
    }
}
