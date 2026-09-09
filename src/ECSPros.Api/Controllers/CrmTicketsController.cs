using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Api.Authorization;
using System.Security.Claims;
using ECSPros.Crm.Application.Tickets.Commands;
using ECSPros.Crm.Application.Tickets.Queries;
using ECSPros.Shared.Infrastructure.Messaging;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// Müşteri İlişkileri (talep/şikayet kayıtları) — eski /crm/musteri-iliskileri-yonetimi (plan docs/crm-musteri-iliskileri-plani.md v2).
/// Yetki: şimdilik panel kullanıcısı olmak yeterli (K8). Bildirim: yalnız panel (SignalR user:{id} + poll).
/// </summary>
[ApiController]
[Route("api/crm/tickets")]
[Authorize]
[RequirePermission(Permissions.CrmTicketsView)]   // Y2: sayfa yetkisi
public class CrmTicketsController(IMediator mediator, ILogger<CrmTicketsController> logger) : ControllerBase
{
    private (Guid Id, string Ad) MevcutKullanici()
    {
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id);
        var ad = User.FindFirstValue("full_name") ?? User.FindFirstValue(ClaimTypes.Email) ?? "Bilinmeyen";
        return (id, ad);
    }
    private static IActionResult Sonuc<T>(ECSPros.Shared.Kernel.Common.Result<T> r)
        => r.IsFailure ? new BadRequestObjectResult(new { success = false, error = r.Error }) : new OkObjectResult(new { success = true, data = r.Value });

    [HttpGet]
    public async Task<IActionResult> List([FromServices] ECSPros.Api.Authorization.IKanalKapsami kanalKapsami,
        [FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, 
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null, [FromQuery] string? type = null,
        [FromQuery] Guid? subjectId = null, [FromQuery] Guid? createdBy = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        [FromQuery] long? trackingNo = null, [FromQuery] string? customer = null, [FromQuery] string? orderNumber = null, [FromQuery] string? search = null,
        [FromQuery] Guid? memberId = null, [FromQuery] Guid? orderId = null, [FromQuery] bool taggedMe = false, [FromQuery] bool unreadByMe = false,
        [FromQuery] bool includeHidden = false, [FromQuery] string? sort = null, CancellationToken ct = default)
    {
        var (uid, _) = MevcutKullanici();
        // DataGrid F4 (2026-09-08): sort/dir + f.* (TicketGrid.Schema beyaz listesi); page/pageSize merkezi clamp
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, await kanalKapsami.KanallarAsync(Permissions.CrmTicketsView, ct), defaultPageSize: 20);
        var sonuc = await mediator.Send(new GetTicketsQuery(uid, grid.Page, grid.PageSize, status, type, subjectId, createdBy, from, to, trackingNo,
            customer, orderNumber, search, memberId, orderId, taggedMe, unreadByMe, includeHidden, sort, grid), ct);
        if (sonuc.IsFailure) return Sonuc(sonuc);

        // Y6 (K6): müşteri/arayan telefonu hassas alandır — yetkisi olmayana maske gider.
        var izin = await alanYetkileri.IzinlerAsync(ct);
        var sayfa = sonuc.Value!;
        var maskeli = sayfa with
        {
            Items = sayfa.Items.Select(t => t with
            {
                CustomerPhone = izin.Telefonla(t.CustomerPhone) ?? "",
                CallerPhone = izin.Telefonla(t.CallerPhone) ?? "",
            }).ToList(),
        };
        return Ok(new { success = true, data = maskeli });
    }

    /// <summary>Talepleri Excel'e aktarır (DataGrid F4): gövdede aynı filtre modeli (search/sort/dir/filters + named: status, type,
    /// subjectId, createdBy, from, to, trackingNo, customer, orderNumber, memberId, orderId, taggedMe, unreadByMe, includeHidden, sort) + kolon listesi.</summary>
    [HttpPost("export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> Export([FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] ECSPros.Shared.Kernel.Grid.GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam, [FromServices] ECSPros.Api.Authorization.IKanalKapsami kanalKapsami, CancellationToken ct)
    {
        var kanalKisiti = await kanalKapsami.KanallarAsync(Permissions.CrmTicketsView, ct);   // Y3: export listeyle aynı kapsamdan geçer
        var (uid, _) = MevcutKullanici();
        var E = ECSPros.Api.Grid.GridExportEndpoint.Tarih; // kısaltma
        var filters = new TicketListFilters(
            body.NamedValue("status"), body.NamedValue("type"), ECSPros.Api.Grid.GridExportEndpoint.Kimlik(body, "subjectId"),
            ECSPros.Api.Grid.GridExportEndpoint.Kimlik(body, "createdBy"), E(body, "from"), E(body, "to"),
            long.TryParse(body.NamedValue("trackingNo"), out var tn) ? tn : null,
            body.NamedValue("customer"), body.NamedValue("orderNumber"), body.Search,
            ECSPros.Api.Grid.GridExportEndpoint.Kimlik(body, "memberId"), ECSPros.Api.Grid.GridExportEndpoint.Kimlik(body, "orderId"),
            ECSPros.Api.Grid.GridExportEndpoint.Bayrak(body, "taggedMe") ?? false, ECSPros.Api.Grid.GridExportEndpoint.Bayrak(body, "unreadByMe") ?? false,
            ECSPros.Api.Grid.GridExportEndpoint.Bayrak(body, "includeHidden") ?? false);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "tickets", "talepler", "Talepler",
            ECSPros.Api.Grid.TicketExportColumns.All, max => mediator.Send(new ExportTicketsQuery(uid, filters, body.ToGridRequest(kanalKisiti), max, body.NamedValue("sort")), ct), ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    [HttpGet("{trackingNo:long}")]
    public async Task<IActionResult> Detail(
        [FromServices] ECSPros.Api.Authorization.IKanalKapsami kanalKapsami, long trackingNo, CancellationToken ct)
    {
        var sonuc = await mediator.Send(new GetTicketDetailQuery(trackingNo), ct);
        // Y3 (K2 + §C.2): kapsam dışı kaydın varlığı sızmasın → 404.
        var kanallar = await kanalKapsami.KanallarAsync(Permissions.CrmTicketsView, ct);
        if (sonuc.IsSuccess && kanallar is not null
            && sonuc.Value!.FirmPlatformId is { } fp && !kanallar.Contains(fp))
            return NotFound(new { success = false, error = "Kayıt bulunamadı." });
        return Sonuc(sonuc);
    }

    /// <summary>Kayıt açıldı: okundu satırları + bu kaydın bildirimlerini "kayda girdi" yap (GET yan etkisiz kalsın diye ayrı uç).</summary>
    [HttpPost("{id:guid}/open")]
    [RequirePermission(Permissions.CrmTicketsManage)]   // Y2
    public async Task<IActionResult> Open(Guid id, CancellationToken ct)
    {
        var (uid, ad) = MevcutKullanici();
        return Sonuc(await mediator.Send(new OpenTicketCommand(id, uid, ad), ct));
    }

    /// <summary>Sipariş numarasından sipariş/müşteri adayları (birden fazla kanal → panel seçtirir, K9).</summary>
    [HttpGet("order-lookup")]
    public async Task<IActionResult> OrderLookup([FromQuery] string number, [FromServices] Services.CrmTicketOrderLookup lookup, CancellationToken ct)
        => Ok(new { success = true, data = await lookup.BulAsync(number ?? "", ct) });

    public record CreateBody(Guid SubjectId, string? CallerName, string? CallerPhone, string? OrderNumber, Guid? OrderId, Guid? FirmPlatformId,
        Guid? MemberId, int? LegacyMemberId, string? CustomerName, string? CustomerPhone, string? BodyHtml, List<string>? Attachments);

    [HttpPost]
    [RequirePermission(Permissions.CrmTicketsManage)]   // Y2
    public async Task<IActionResult> Create([FromBody] CreateBody b, [FromServices] Services.CrmTicketOrderLookup lookup, CancellationToken ct)
    {
        var (uid, ad) = MevcutKullanici();
        // sipariş anlık görüntüsü sunucuda çözülür (panelden gelen müşteri bilgisi yalnız sipariş bulunamazsa kullanılır)
        Guid? orderId = b.OrderId; Guid? platformId = b.FirmPlatformId; Guid? memberId = b.MemberId; int? legacyMemberId = b.LegacyMemberId;
        string? customerName = b.CustomerName, customerPhone = b.CustomerPhone;
        if (!string.IsNullOrWhiteSpace(b.OrderNumber))
        {
            var adaylar = await lookup.BulAsync(b.OrderNumber, ct);
            if (adaylar.Count > 1 && b.OrderId is null && b.FirmPlatformId is null)
                return BadRequest(new { success = false, error = "Bu sipariş numarası birden fazla kanalda bulundu; kanal seçin.", code = "channel_required", data = adaylar });
            var secilen = adaylar.Count == 1 ? adaylar[0]
                : adaylar.FirstOrDefault(a => a.OrderId == b.OrderId) ?? adaylar.FirstOrDefault(a => a.FirmPlatformId == b.FirmPlatformId);
            if (secilen is not null)
            {
                orderId = secilen.OrderId; platformId = secilen.FirmPlatformId; memberId = secilen.MemberId; legacyMemberId = secilen.LegacyMemberId;
                customerName = secilen.CustomerName; customerPhone = secilen.CustomerPhone;
            }
        }
        return Sonuc(await mediator.Send(new CreateTicketCommand(b.SubjectId, b.CallerName, b.CallerPhone, b.OrderNumber, orderId, platformId,
            memberId, legacyMemberId, customerName, customerPhone, b.BodyHtml, b.Attachments, uid, ad), ct));
    }

    public record ActivityBody(string? BodyHtml, List<string>? Attachments, Guid StatusId, Guid? TaggedUserId, string? TaggedUserName);

    [HttpPost("{id:guid}/activities")]
    [RequirePermission(Permissions.CrmTicketsManage)]   // Y2
    public async Task<IActionResult> AddActivity(Guid id, [FromBody] ActivityBody b, [FromServices] IRealtimeNotificationService rt, CancellationToken ct)
    {
        var (uid, ad) = MevcutKullanici();
        var result = await mediator.Send(new AddTicketActivityCommand(id, b.BodyHtml, b.Attachments, b.StatusId, b.TaggedUserId, b.TaggedUserName, uid, ad), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        // panel çanı: her hedef kullanıcıya kendi grubu üzerinden (hub hatası kaydı düşürmez)
        foreach (var n in result.Value.Notifications)
        {
            try { await rt.SendUserNotificationAsync(n.UserId.ToString(), "TicketNotification", new { n.NotificationId, ticketId = id, result.Value.TrackingNo, n.Kind, n.Message }, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "CRM bildirimi hub'a gönderilemedi: {User}", n.UserId); }
        }
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("{id:guid}/hidden")]
    [RequirePermission(Permissions.CrmTicketsManage)]   // Y2
    public async Task<IActionResult> SetHidden(Guid id, [FromBody] bool hidden, CancellationToken ct)
    {
        var (uid, ad) = MevcutKullanici();
        return Sonuc(await mediator.Send(new SetTicketHiddenCommand(id, hidden, uid, ad), ct));
    }

    // ── bildirimler (çan) ──
    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool onlyPending = false, CancellationToken ct = default)
    {
        var (uid, _) = MevcutKullanici();
        return Sonuc(await mediator.Send(new GetMyTicketNotificationsQuery(uid, page, pageSize, onlyPending), ct));
    }

    [HttpPost("notifications/seen")]
    [RequirePermission(Permissions.CrmTicketsManage)]   // Y2
    public async Task<IActionResult> NotificationsSeen([FromBody] List<Guid> ids, CancellationToken ct)
    {
        var (uid, _) = MevcutKullanici();
        return Sonuc(await mediator.Send(new MarkTicketNotificationsSeenCommand(uid, ids ?? []), ct));
    }

    [HttpPost("notifications/{id:guid}/opened")]
    [RequirePermission(Permissions.CrmTicketsManage)]   // Y2
    public async Task<IActionResult> NotificationOpened(Guid id, CancellationToken ct)
    {
        var (uid, _) = MevcutKullanici();
        return Sonuc(await mediator.Send(new MarkTicketNotificationOpenedCommand(uid, id), ct));
    }

    // ── ayarlar: durumlar + konular ──
    [HttpGet("settings")]
    public async Task<IActionResult> Settings([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Sonuc(await mediator.Send(new GetTicketSettingsQuery(includeInactive), ct));

    public record SubjectBody(Guid? Id, string Name, string Type, int SortOrder, bool IsActive, List<string>? RequiredFields);
    [HttpPost("settings/subjects")]
    [RequirePermission(Permissions.CrmTicketsManage)]   // Y2
    public async Task<IActionResult> SaveSubject([FromBody] SubjectBody b, CancellationToken ct)
        => Sonuc(await mediator.Send(new UpsertTicketSubjectCommand(b.Id, b.Name, b.Type, b.SortOrder, b.IsActive, b.RequiredFields ?? []), ct));

    public record StatusBody(Guid? Id, string Code, string Name, string Color, int SortOrder, bool IsHidden, bool IsResolved, bool ExemptFromDuplicateCheck, bool IsDefault);
    [HttpPost("settings/statuses")]
    [RequirePermission(Permissions.CrmTicketsManage)]   // Y2
    public async Task<IActionResult> SaveStatus([FromBody] StatusBody b, CancellationToken ct)
        => Sonuc(await mediator.Send(new UpsertTicketStatusCommand(b.Id, b.Code, b.Name, b.Color, b.SortOrder, b.IsHidden, b.IsResolved, b.ExemptFromDuplicateCheck, b.IsDefault), ct));

    /// <summary>Ek yükleme — Requests.UploadMedia kopyası; dosyalar media/crm/yyyyMM altına.</summary>
    [HttpPost("media")]
    [RequirePermission(Permissions.CrmTicketsManage)]   // Y2
    [RequestSizeLimit(11_000_000)]
    public async Task<IActionResult> UploadMedia(IFormFile? file, [FromServices] Services.Storage.IFileStorage storage, CancellationToken ct)
    {
        var uzantilar = new Dictionary<string, string>
        {
            ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp", ["image/gif"] = ".gif", ["application/pdf"] = ".pdf",
        };
        if (file is null || file.Length == 0) return BadRequest(new { success = false, error = "Dosya gönderilmedi." });
        if (file.Length > 10_000_000) return BadRequest(new { success = false, error = "Dosya en fazla 10 MB olabilir." });
        if (!uzantilar.TryGetValue(file.ContentType, out var uzanti)) return BadRequest(new { success = false, error = "Yalnızca JPEG, PNG, WebP, GIF veya PDF yükleyebilirsiniz." });
        await using var stream = file.OpenReadStream();
        var stored = await storage.SavePublicAsync($"crm/{DateTime.UtcNow:yyyyMM}", $"{Guid.NewGuid():N}{uzanti}", stream, file.ContentType, ct);
        return Ok(new { success = true, data = new { url = stored.PublicUrl } });
    }
}
