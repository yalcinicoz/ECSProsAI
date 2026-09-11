using ECSPros.Promotion.Application.Games;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// Şans oyunları — docs/BACKEND_OYUNLAR.md (mobil ekip sözleşmesi, 2026-09-11). Kanal kimliği X-Firm-Platform başlığı
/// ya da ?firmPlatformId (FirmPlatformHeaderFilter). Liste anonim (misafir kartı görür, `login_required` alır);
/// oynama ve geçmiş YALNIZ üye (v1 kararı: misafir oynayamaz — kupon üyeye bağlanır).
/// </summary>
[ApiController]
[Route("api/store/games")]
public class StoreGamesController(IMediator mediator, ECSPros.Api.Services.IStoreContext storeContext) : ControllerBase
{
    private Guid? MemberIdOrNull()
    {
        if (User.FindFirst("type")?.Value != "member") return null;
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var mid) ? mid : null;
    }

    private async Task<Guid?> PlatformAsync(Guid? firmPlatformId, CancellationToken ct)
        => firmPlatformId is { } f && f != Guid.Empty ? f : (await storeContext.GetPlatformAsync(ct))?.Id;

    /// <summary>Bugün aktif oyun(lar) + bu kullanıcı için durum. Aktif oyun yoksa boş liste (mobilde ikon çıkmaz).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List([FromQuery] Guid? firmPlatformId, CancellationToken ct)
    {
        var platform = await PlatformAsync(firmPlatformId, ct);
        if (platform is null) return BadRequest(new { success = false, error = "Kanal çözülemedi (firmPlatformId)." });
        var result = await mediator.Send(new GetStoreGamesQuery(platform.Value, MemberIdOrNull()), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Oyna: sunucu ödülü seçer, kuponu/puanı işler, hakkı düşer. Hak bitmişse aynı dönemin son sonucu aynen döner (idempotent).</summary>
    [HttpPost("{code}/play")]
    [AllowAnonymous]   // cihaz token'ıyla gelen misafire politika 403'ü yerine sözleşmeye uygun {success:false,error} (401) dönmek için
    public async Task<IActionResult> Play(string code, [FromQuery] Guid? firmPlatformId, CancellationToken ct)
    {
        var platform = await PlatformAsync(firmPlatformId, ct);
        if (platform is null) return BadRequest(new { success = false, error = "Kanal çözülemedi (firmPlatformId)." });
        var memberId = MemberIdOrNull();
        if (memberId is null) return StatusCode(401, new { success = false, error = "Oynamak için giriş yapmalısınız." });
        var result = await mediator.Send(new ECSPros.Api.Handlers.PlayGameCommand(platform.Value, code, memberId.Value), ct);
        if (result.IsFailure) return StatusCode(409, new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Üyenin oynanışları (isteğe bağlı uç; Kuponlarım kuponları zaten gösterir).</summary>
    [HttpGet("/api/store/account/games/history")]
    [Authorize(Policy = "MemberOnly")]
    public async Task<IActionResult> History([FromQuery] Guid? firmPlatformId, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var platform = await PlatformAsync(firmPlatformId, ct);
        if (platform is null) return BadRequest(new { success = false, error = "Kanal çözülemedi (firmPlatformId)." });
        var result = await mediator.Send(new GetMemberGameHistoryQuery(platform.Value, MemberIdOrNull()!.Value, limit), ct);
        return Ok(new { success = true, data = result.Value });
    }
}
