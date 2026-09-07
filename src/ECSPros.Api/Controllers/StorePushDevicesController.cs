using ECSPros.Storefront.Application.Commands.PushDevices;
using ECSPros.Storefront.Application.Queries.PushDevices;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECSPros.Api.Controllers;

/// <summary>
/// Mobil push bildirim cihaz kaydı (2026-09-05): uygulama FCM/APNs token'ını buraya
/// yazar; bildirim gönderimi bu adres defterinden beslenir. Kayıt anonim de yapılabilir
/// (bildirim izni girişten önce istenebilir) — girişliyken gönderilen kayıt üyeye
/// bağlanır, çıkış SONRASI yeniden gönderim bağlantıyı koparır. Token her değiştiğinde
/// ve her giriş/çıkışta yeniden POST edilmelidir (upsert, idempotent).
/// </summary>
[ApiController]
[Route("api/store/push-devices")]
[EnableRateLimiting("store-auth")]
public class StorePushDevicesController(IMediator mediator) : ControllerBase
{
    /// <summary>Girişli ÜYE isteğiyse üye kimliği; anonim/diğer kimliklerde null.</summary>
    private Guid? UyeKimligi()
    {
        if (User.FindFirst("type")?.Value != "member") return null;
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    /// <summary>Cihaz kaydı/güncellemesi (anonim veya üye). Aynı token'a upsert; aynı
    /// deviceId yeni token'la gelirse eski token kayıtları revoked olur.</summary>
    /// <param name="req">platform: android|ios; token: FCM/APNs push token;
    /// deviceId: uygulama kurulum kimliği (token rotasyonu eşleşmesi için önerilir).</param>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] PushDeviceRegisterRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new RegisterPushDeviceCommand(
            req.FirmPlatformId, UyeKimligi(), req.Platform ?? "", req.Token ?? "",
            req.DeviceId, req.AppVersion), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Kaydı iptal eder (çıkışta / bildirim izni kapatılınca). İdempotent —
    /// token zaten yoksa da başarı döner.</summary>
    [HttpPost("revoke")]
    [AllowAnonymous]
    public async Task<IActionResult> Revoke([FromBody] PushDeviceRevokeRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new RevokePushDeviceCommand(req.FirmPlatformId, req.Token ?? ""), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Üyenin kayıtlı cihazları — token maskeli (son 8 karakter) döner.</summary>
    /// <summary>Bildirime tıklandı (data.dedupId geri yollanır) → push_notifications.OpenedAt. Üye JWT'siyle üyenin, değilse token'ın satırı.</summary>
    [HttpPost("opened")]
    public async Task<IActionResult> Opened([FromBody] PushOpenedRequest req, [FromServices] ECSPros.Storefront.Application.Services.IStorefrontDbContext sdb, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req?.DedupId)) return BadRequest(new { success = false, error = "dedupId zorunlu." });
        var mid = UyeKimligi();
        var q = sdb.PushNotifications.Where(n => n.DedupId == req.DedupId && n.OpenedAt == null);
        if (mid is { } m) q = q.Where(n => n.MemberId == m);
        else if (!string.IsNullOrWhiteSpace(req.Token)) { var h = ECSPros.Api.Services.Push.PushKuyruk.Hash(req.Token); q = q.Where(n => n.TokenHash == h); }
        var rows = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(q, ct);
        foreach (var n in rows) n.OpenedAt = DateTime.UtcNow;
        if (rows.Count > 0) await sdb.SaveChangesAsync(ct);
        return Ok(new { success = true, data = rows.Count });
    }

    [HttpGet("mine")]
    [Authorize(Policy = "MemberOnly")]
    public async Task<IActionResult> GetMine([FromQuery] Guid firmPlatformId, CancellationToken ct)
    {
        var uye = UyeKimligi();
        if (uye is null) return Unauthorized();
        var result = await mediator.Send(new GetMemberPushDevicesQuery(firmPlatformId, uye.Value), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}

public record PushDeviceRegisterRequest(
    Guid FirmPlatformId, string? Platform, string? Token, string? DeviceId, string? AppVersion);

public record PushDeviceRevokeRequest(Guid FirmPlatformId, string? Token);
public record PushOpenedRequest(string? DedupId, string? Token);
