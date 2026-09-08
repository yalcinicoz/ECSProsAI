using ECSPros.Api.Services.Store;
using ECSPros.Storefront.Application.Queries.GetNewsletterSubscriptions;
using ECSPros.Storefront.Application.Queries.GetSavedSearchesForAdmin;
using ECSPros.Storefront.Application.Queries.GetStockAlertsForAdmin;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Controllers;

/// <summary>
/// H8: bildirim operasyonları (admin). Favori arama taraması normalde
/// SavedSearchNotifyWorker'la periyodik koşar — buradaki tetik, "şimdi tara" operasyon
/// ihtiyacı + E2E determinizmi içindir (günde-1 sınırı LastNotifiedAt'ta olduğundan
/// elle tetiklemek yinelenen e-posta üretmez).
/// P5: izleme listeleri — stok alarmları, kayıtlı aramalar, bülten aboneleri.
/// </summary>
[ApiController]
[Route("api/store-notifications")]
[Authorize]
public class StoreNotificationsController(
    ISavedSearchNotifier savedSearchNotifier,
    IMediator mediator) : ControllerBase
{
    [HttpPost("saved-search-scan")]
    public async Task<IActionResult> RunSavedSearchScan(CancellationToken ct)
    {
        var gonderilen = await savedSearchNotifier.RunOnceAsync(ct);
        return Ok(new { success = true, data = new { sent = gonderilen } });
    }

    [HttpGet("stock-alerts")]
    public async Task<IActionResult> GetStockAlerts(
        [FromQuery] string? status = null,
        [FromQuery] Guid? firmPlatformId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetStockAlertsForAdminQuery(status, firmPlatformId, search, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("saved-searches")]
    public async Task<IActionResult> GetSavedSearches(
        [FromQuery] bool? notifyEnabled = null,
        [FromQuery] Guid? firmPlatformId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetSavedSearchesForAdminQuery(notifyEnabled, firmPlatformId, search, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("newsletter-subscriptions")]
    public async Task<IActionResult> GetNewsletterSubscriptions(
        [FromQuery] bool? isActive = null,
        [FromQuery] Guid? firmPlatformId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetNewsletterSubscriptionsQuery(isActive, firmPlatformId, search, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Mobil push cihaz kayıtları (2026-09-05) — bildirim gönderim/izleme
    /// yüzeyi: üye filtresiyle üyenin cihaz token'ları TAM haliyle döner (panel üye
    /// detayı + operasyon). Store tarafındaki 'mine' ucu maskeli verir, bu uç admin'dir.</summary>
    // ── Mobil push (docs/PUSH_BILDIRIM_ENTEGRASYONU.md, 2026-09-07): şablonlar, gönderim logu, tek cihaza test ──
    [HttpGet("push-templates")]
    public async Task<IActionResult> PushTemplates([FromServices] ECSPros.Storefront.Application.Services.IStorefrontDbContext sdb, CancellationToken ct)
        => Ok(new { success = true, data = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            sdb.PushTemplates.AsNoTracking().OrderBy(t => t.Class).ThenBy(t => t.Name).Select(t => new { t.Id, t.Type, t.Class, t.Name, t.Title, t.Body, t.LinkTemplate, t.Enabled, t.TtlSeconds, t.Priority, t.Description, t.Inbox, t.Icon, t.ExpiresDays, t.DismissOnOpen }), ct) });

    /// <summary>Bildirimlerim alanları (2026-09-08): inbox (listede göster), icon (§5 anahtarı), expiresDays (listede kalma), dismissOnOpen.</summary>
    public record PushTemplateBody(string? Name, string Title, string Body, string LinkTemplate, bool Enabled, int? TtlSeconds, string? Priority,
        bool? Inbox = null, string? Icon = null, int? ExpiresDays = null, bool? DismissOnOpen = null);

    [HttpPut("push-templates/{type}")]
    public async Task<IActionResult> SavePushTemplate(string type, [FromBody] PushTemplateBody b, [FromServices] ECSPros.Storefront.Application.Services.IStorefrontDbContext sdb, CancellationToken ct)
    {
        var t = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(sdb.PushTemplates, x => x.Type == type, ct);
        if (t is null) return NotFound(new { success = false, error = "Şablon bulunamadı." });
        if (string.IsNullOrWhiteSpace(b.Title) || string.IsNullOrWhiteSpace(b.Body)) return BadRequest(new { success = false, error = "Başlık ve gövde zorunludur." });
        // link şablonu: yer tutucular örnek değerle doldurulup katalogla doğrulanır
        var ornek = ECSPros.Api.Services.Push.PushKuyruk.Doldur(b.LinkTemplate ?? "/", new Dictionary<string, string> { ["orderId"] = Guid.Empty.ToString(), ["productCode"] = "P-00000001", ["slug"] = "ornek" });
        if (ECSPros.Api.Services.Push.PushLinkKatalogu.Normalize(ornek) is null) return BadRequest(new { success = false, error = "Link uygulamanın tanıdığı yollardan biri değil (§3 link kataloğu; /odeme ve /teslimat kullanılamaz)." });
        if (!string.IsNullOrWhiteSpace(b.Name)) t.Name = b.Name.Trim();
        t.Title = b.Title.Trim(); t.Body = b.Body.Trim(); t.LinkTemplate = (b.LinkTemplate ?? "/").Trim(); t.Enabled = b.Enabled;
        if (b.TtlSeconds is > 0) t.TtlSeconds = b.TtlSeconds.Value;
        if (b.Priority is "high" or "normal") t.Priority = b.Priority;
        if (b.Inbox is { } inbox) t.Inbox = inbox;
        if (b.Icon is { Length: > 0 } icon) { if (!ECSPros.Api.Services.Push.BildirimKutusu.Ikonlar.Contains(icon)) return BadRequest(new { success = false, error = "İkon anahtarı geçersiz: " + string.Join(", ", ECSPros.Api.Services.Push.BildirimKutusu.Ikonlar) }); t.Icon = icon; }
        if (b.ExpiresDays is { } gun) { if (gun is < 1 or > 365) return BadRequest(new { success = false, error = "Listede kalma süresi 1-365 gün olmalı." }); t.ExpiresDays = gun; }
        if (b.DismissOnOpen is { } d) t.DismissOnOpen = d;
        await sdb.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpGet("push-log")]
    public async Task<IActionResult> PushLog([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? type = null, [FromQuery] string? status = null,
        [FromQuery] Guid? memberId = null, [FromServices] ECSPros.Storefront.Application.Services.IStorefrontDbContext sdb = null!, CancellationToken ct = default)
    {
        var q = sdb.PushNotifications.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(n => n.Type == type);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(n => n.Status == status);
        if (memberId is not null) q = q.Where(n => n.MemberId == memberId);
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(q, ct);
        var items = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(q.OrderByDescending(n => n.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new { n.Id, n.MemberId, n.DeviceId, n.Platform, n.Type, n.Class, n.DedupId, n.Title, n.Body, n.Link, n.Status, n.ErrorCode, n.Attempts, n.ScheduledAt, n.SentAt, n.OpenedAt, n.CreatedAt, n.Inbox, n.Icon, n.ReadAt, n.DismissedAt, n.ExpiresAt }), ct);
        var ozet = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            sdb.PushNotifications.AsNoTracking().Where(n => n.CreatedAt >= DateTime.UtcNow.AddDays(-1)).GroupBy(n => n.Status).Select(g => new { status = g.Key, count = g.Count() }), ct);
        return Ok(new { success = true, data = new { items, totalCount = total, page, pageSize, last24h = ozet } });
    }

    public record PushTestBody(Guid? DeviceId, Guid? MemberId, string? Type, string? Title, string? Body, string? Link);

    /// <summary>§9 test: TEK cihaza (deviceId) ya da üyenin cihazlarına deneme bildirimi; şablon tipi verilirse şablon metni, yoksa serbest metin. Konsoldan toplu kampanya ASLA.</summary>
    [HttpPost("push-test")]
    public async Task<IActionResult> PushTest([FromBody] PushTestBody b, [FromServices] ECSPros.Api.Services.Push.PushKuyruk kuyruk, CancellationToken ct)
    {
        if (b.DeviceId is null && b.MemberId is null) return BadRequest(new { success = false, error = "deviceId ya da memberId gerekli." });
        var tip = string.IsNullOrWhiteSpace(b.Type) ? "test" : b.Type!;
        var vars = new Dictionary<string, string> { ["orderNumber"] = "MIS0000000", ["orderId"] = Guid.Empty.ToString(), ["productName"] = "Deneme Ürünü", ["productCode"] = "P-00000001", ["cargoName"] = "Kargo", ["trackingNumber"] = "123456", ["newPrice"] = "199,90 ₺", ["oldPrice"] = "249,90 ₺", ["n"] = "2", ["couponCode"] = "TEST10", ["discountText"] = "%10 indirim", ["expiresAt"] = DateTime.Today.AddDays(1).ToString("dd.MM.yyyy"), ["amount"] = "50,00 ₺", ["variantInfo"] = "M / Siyah", ["returnStatusLabel"] = "onaylandı" };
        var n = await kuyruk.EnqueueAsync(new ECSPros.Api.Services.Push.PushIstek(tip, b.MemberId, vars, $"test:{Guid.NewGuid():N}", DeviceId: b.DeviceId,
            TitleOverride: tip == "test" ? (b.Title ?? "Deneme bildirimi") : b.Title, BodyOverride: tip == "test" ? (b.Body ?? "Bu bir deneme bildirimidir.") : b.Body, LinkOverride: b.Link), ct);
        return Ok(new { success = true, data = new { queued = n, note = n == 0 ? "Kuyruğa alınmadı: aktif cihaz yok, şablon kapalı ya da pazarlama izni/sıklık kuralı engelledi." : "Kuyruğa alındı; worker 15 sn içinde gönderir (FCM servis hesabı tanımlı olmalı)." } });
    }

    [HttpGet("push-devices")]
    public async Task<IActionResult> GetPushDevices(
        [FromQuery] Guid? firmPlatformId = null,
        [FromQuery] Guid? memberId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new ECSPros.Storefront.Application.Queries.PushDevices
            .GetPushDevicesForAdminQuery(firmPlatformId, memberId, status, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}
