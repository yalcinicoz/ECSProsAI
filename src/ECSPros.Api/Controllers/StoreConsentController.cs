using ECSPros.Api.Services;
using ECSPros.Api.Services.Tracking;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Controllers;

/// <summary>
/// İE-6 Faz F (2026-08-22): çerez/consent tercih kaydı. Banner/ayar ekranı her tercihte POST eder
/// (ispat günlüğü, 12 ay); üye girişliyse MemberId yazılır → sonraki cihaz/oturumda SSR üyenin son
/// tercihini çerez yokken uygular. Mobil uygulama kendi izin ekranından aynı ucu çağırır (source=mobile).
/// Hata-güvenli: kayıt başarısız olsa bile 200 (istemci davranışı değişmez).
/// </summary>
[ApiController]
[Route("api/store/consent")]
[EnableRateLimiting("store-sensitive")]
public class StoreConsentController(
    IIntegrationDbContext db,
    IStoreContext storeContext,
    ILogger<StoreConsentController> logger) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Kaydet([FromBody] StoreConsentRequest req, CancellationToken ct)
    {
        try
        {
            var platformId = req.FirmPlatformId ?? (await storeContext.GetPlatformAsync(ct))?.Id;
            if (platformId is null || platformId == Guid.Empty) return Ok(new { success = true });
            Guid? memberId = null;
            if (User.FindFirst("type")?.Value == "member")
            {
                var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(sub, out var mid)) memberId = mid;
            }
            var ip = TrackingHttpContextReader.IstemciIp(HttpContext);
            var ua = HttpContext.Request.Headers.UserAgent.ToString();
            db.TrackingConsentLogs.Add(new TrackingConsentLog
            {
                FirmPlatformId = platformId.Value,
                ConsentId = string.IsNullOrWhiteSpace(req.ConsentId) ? Guid.NewGuid().ToString("N") : req.ConsentId.Trim()[..Math.Min(64, req.ConsentId.Trim().Length)],
                MemberId = memberId,
                Analytics = req.Analytics, Ads = req.Ads, Personalization = req.Personalization,
                Source = req.Source is "settings" or "member_sync" or "mobile" ? req.Source : "banner",
                IpHash = TrackingHttpContextReader.Sha256(ip),
                UserAgent = string.IsNullOrWhiteSpace(ua) ? null : (ua.Length > 500 ? ua[..500] : ua)
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Consent kaydı yazılamadı.");
        }
        return Ok(new { success = true });
    }

    /// <summary>Üyenin son tercihi (mobil uygulama açılışı / hesabım ekranı).</summary>
    [HttpGet("me")]
    [Authorize(Policy = "MemberOnly")]
    public async Task<IActionResult> Benim(CancellationToken ct)
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var memberId)) return Ok(new { success = true, data = (object?)null });
        var son = await db.TrackingConsentLogs.AsNoTracking().Where(c => c.MemberId == memberId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new { c.Analytics, c.Ads, c.Personalization, c.CreatedAt, c.Source })
            .FirstOrDefaultAsync(ct);
        return Ok(new { success = true, data = son });
    }
}

public record StoreConsentRequest(
    bool Analytics, bool Ads, bool Personalization,
    string? ConsentId = null, Guid? FirmPlatformId = null, string? Source = null);
