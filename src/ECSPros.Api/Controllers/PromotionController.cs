using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Api.Authorization;
using ECSPros.Promotion.Application.Commands.CopyCampaign;
using ECSPros.Promotion.Application.Commands.CreateCampaign;
using ECSPros.Promotion.Application.Commands.ManageCoupon;
using ECSPros.Promotion.Application.Commands.UpdateCampaign;
using ECSPros.Promotion.Application.Queries.GetCampaignDetail;
using ECSPros.Promotion.Application.Commands.UseCoupon;
using ECSPros.Promotion.Application.Queries.CalculateDiscounts;
using ECSPros.Promotion.Application.Queries.GetCampaigns;
using ECSPros.Promotion.Application.Queries.GetCampaignTypes;
using ECSPros.Promotion.Application.Queries.GetCoupons;
using ECSPros.Promotion.Application.Queries.GetCouponUsages;
using ECSPros.Promotion.Application.Queries.ValidateCoupon;
using ECSPros.Promotion.Application.Services.Engine;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/promotion")]
[Authorize]
[RequirePermission(Permissions.PromotionView)]   // Y2: sayfa yetkisi
public class PromotionController : ControllerBase
{
    private readonly IMediator _mediator;
    // Kampanya ekleme/düzenleme/silme denetim kaydı (2026-09-11 kullanıcı isteği): iam.audit_logs'a
    // EntityType=Campaign, Action=Created/Updated/Deleted, eski+yeni değer = tam detay DTO'su
    // (ad, tarih, ayarlar, kapsam, manuel ürün listesi). Yazıcı vitrin için yazılmıştı, genel amaçlıdır.
    private readonly ECSPros.Api.Services.Store.IVitrinAuditLogger _audit;

    public PromotionController(IMediator mediator, ECSPros.Api.Services.Store.IVitrinAuditLogger audit)
    {
        _mediator = mediator;
        _audit = audit;
    }

    private async Task<CampaignDetailDto?> KampanyaAnlikAsync(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetCampaignDetailQuery(id), ct);
        return r.IsSuccess ? r.Value : null;
    }

    private Task KampanyaLogAsync(string action, Guid id, CampaignDetailDto? eski, CampaignDetailDto? yeni, CancellationToken ct)
    {
        var d = yeni ?? eski;
        var baslik = d is null ? null : $"{(d.NameI18n.TryGetValue("tr", out var ad) ? ad : d.Code)} [{d.Code}]";
        return _audit.LogAsync(HttpContext, action, "Campaign", id, eski, yeni, d?.FirmPlatformId ?? Guid.Empty, baslik, ct);
    }

    /// <summary>Kampanyaları listeler.</summary>
    [HttpGet("campaigns")]
    public async Task<IActionResult> GetCampaigns([FromServices] ECSPros.Api.Authorization.IKanalKapsami kanalKapsami, 
        [FromQuery] bool activeOnly = true, [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        // DataGrid F4 (2026-09-08): `page` parametresi varsa sayfalı+filtreli+sıralı (CampaignGrid.Schema); yoksa eski düz dizi (diğer çağıranlar bozulmaz)
        if (Request.Query.ContainsKey("page"))
        {
            var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, await kanalKapsami.KanallarAsync(Permissions.PromotionView, ct), defaultPageSize: 50);
            var paged = await _mediator.Send(new GetCampaignsGridQuery(new CampaignListFilters(activeOnly, search), grid), ct);
            return Ok(new { success = true, data = paged.Value });
        }
        var result = await _mediator.Send(new GetCampaignsQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kampanyaları Excel'e aktarır (DataGrid F4): gövde search/sort/dir/filters/columns + named: activeOnly.</summary>
    [HttpPost("campaigns/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportCampaigns([FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] ECSPros.Shared.Kernel.Grid.GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<PromotionController> logger, [FromServices] ECSPros.Api.Authorization.IKanalKapsami kanalKapsami, CancellationToken ct)
    {
        var kanalKisiti = await kanalKapsami.KanallarAsync(Permissions.PromotionView, ct);   // Y3: export listeyle aynı kapsamdan geçer
        var filters = new CampaignListFilters(ECSPros.Api.Grid.GridExportEndpoint.Bayrak(body, "activeOnly") ?? false, body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "campaigns", "kampanyalar", "Kampanyalar",
            ECSPros.Api.Grid.CampaignExportColumns.All, max => _mediator.Send(new ExportCampaignsQuery(filters, body.ToGridRequest(kanalKisiti), max), ct), ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    /// <summary>Kampanya detayı (düzenleme formu için — ürün kapsamı dahil).</summary>
    [HttpGet("campaigns/{id:guid}")]
    public async Task<IActionResult> GetCampaignDetail(
        [FromServices] ECSPros.Api.Authorization.IKanalKapsami kanalKapsami, Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCampaignDetailQuery(id), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });

        // Y3 (K2 + §C.2): kapsam dışı kampanyanın VARLIĞI sızmasın → 403 değil 404.
        var kanallar = await kanalKapsami.KanallarAsync(Permissions.PromotionView, ct);
        if (kanallar is not null && !kanallar.Contains(result.Value!.FirmPlatformId))
            return NotFound(new { success = false, error = "Kampanya bulunamadı." });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni kampanya oluşturur.</summary>
    [HttpPost("campaigns")]
    [RequirePermission(Permissions.PromotionManage)]   // Y2
    public async Task<IActionResult> CreateCampaign([FromBody] CreateCampaignRequest request, CancellationToken ct)
    {
        Guid.TryParse(User.FindFirst("sub")?.Value, out var userId);
        var result = await _mediator.Send(new CreateCampaignCommand(
            request.FirmPlatformId,
            request.CampaignTypeId,
            request.Code,
            request.NameI18n,
            request.DescriptionI18n,
            request.BadgeLabel,
            request.BadgeColor,
            request.StartsAt,
            request.EndsAt,
            request.Priority,
            request.IsActive,
            request.Settings ?? new Dictionary<string, object>(),
            request.FillType,
            request.FilterDef,
            request.ManualProductIds,
            request.ExcludedProductIds,
            userId == Guid.Empty ? null : userId), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        await KampanyaLogAsync("Created", result.Value, null, await KampanyaAnlikAsync(result.Value, ct), ct);
        return Created($"/api/promotion/campaigns", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Kampanya günceller.</summary>
    [HttpPut("campaigns/{id:guid}")]
    [RequirePermission(Permissions.PromotionManage)]   // Y2
    public async Task<IActionResult> UpdateCampaign(Guid id, [FromBody] UpdateCampaignRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId))
            return Unauthorized();

        var eski = await KampanyaAnlikAsync(id, ct);
        var result = await _mediator.Send(new UpdateCampaignCommand(
            id,
            request.NameI18n,
            request.DescriptionI18n,
            request.BadgeLabel,
            request.BadgeColor,
            request.StartsAt,
            request.EndsAt,
            request.IsActive,
            request.Priority,
            request.Settings ?? new Dictionary<string, object>(),
            request.FillType,
            request.FilterDef,
            request.ManualProductIds,
            request.ExcludedProductIds,
            userId), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        await KampanyaLogAsync("Updated", id, eski, await KampanyaAnlikAsync(id, ct), ct);
        return Ok(new { success = true });
    }

    /// <summary>Kampanyayı siler (soft delete). Yalnız hiçbir siparişte kullanılmamış kampanya silinebilir;
    /// yayındaki kampanya için uyarı panelde verilir. Silme denetim kaydına eski değerlerle yazılır.</summary>
    [HttpDelete("campaigns/{id:guid}")]
    [RequirePermission(Permissions.PromotionManage)]
    public async Task<IActionResult> DeleteCampaign(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)) return Unauthorized();
        var eski = await KampanyaAnlikAsync(id, ct);
        var result = await _mediator.Send(new ECSPros.Api.Handlers.DeleteCampaignCommand(id, userId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        await KampanyaLogAsync("Deleted", id, eski, null, ct);
        return Ok(new { success = true });
    }

    /// <summary>Kampanyayı kopyalar (yeni kod, opsiyonel başka platform; kopya pasif başlar).</summary>
    [HttpPost("campaigns/{id:guid}/copy")]
    [RequirePermission(Permissions.PromotionManage)]   // Y2
    public async Task<IActionResult> CopyCampaign(Guid id, [FromBody] CopyCampaignRequest request, CancellationToken ct)
    {
        Guid.TryParse(User.FindFirst("sub")?.Value, out var userId);
        var result = await _mediator.Send(new CopyCampaignCommand(
            id, request.NewCode, request.TargetFirmPlatformId, userId == Guid.Empty ? null : userId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        await KampanyaLogAsync("Created", result.Value, null, await KampanyaAnlikAsync(result.Value, ct), ct);
        return Ok(new { success = true, data = new { id = result.Value } });
    }

    // ── Şans oyunları (docs/BACKEND_OYUNLAR.md, 2026-09-11) — panel CRUD; oynanış + ödül verme mağaza ucunda ──
    [HttpGet("games")]
    public async Task<IActionResult> GetGames([FromServices] ECSPros.Api.Authorization.IKanalKapsami kanalKapsami, [FromQuery] string? search, CancellationToken ct)
    {
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, await kanalKapsami.KanallarAsync(Permissions.PromotionView, ct), defaultPageSize: 50);
        var result = await _mediator.Send(new ECSPros.Promotion.Application.Games.GetGamesQuery(search, grid.Page, grid.PageSize, grid), ct);
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("games/{id:guid}")]
    public async Task<IActionResult> GetGame([FromServices] ECSPros.Api.Authorization.IKanalKapsami kanalKapsami, Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ECSPros.Promotion.Application.Games.GetGameDetailQuery(id), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        var kanallar = await kanalKapsami.KanallarAsync(Permissions.PromotionView, ct);
        if (kanallar is not null && !kanallar.Contains(result.Value!.FirmPlatformId)) return NotFound(new { success = false, error = "Oyun bulunamadı." });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("games/{id:guid}/plays")]
    public async Task<IActionResult> GetGamePlays(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ECSPros.Promotion.Application.Games.GetGamePlaysQuery(id, Math.Max(1, page), Math.Clamp(pageSize, 1, 250)), ct);
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("games")]
    [RequirePermission(Permissions.PromotionManage)]
    public async Task<IActionResult> CreateGame([FromBody] SaveGameRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)) return Unauthorized();
        var result = await _mediator.Send(req.ToCommand(null, userId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        await _audit.LogAsync(HttpContext, "Created", "Game", result.Value, null, req, req.FirmPlatformId, req.Code, ct);
        return Created($"/api/promotion/games/{result.Value}", new { success = true, data = new { id = result.Value } });
    }

    [HttpPut("games/{id:guid}")]
    [RequirePermission(Permissions.PromotionManage)]
    public async Task<IActionResult> UpdateGame(Guid id, [FromBody] SaveGameRequest req, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)) return Unauthorized();
        var eski = await _mediator.Send(new ECSPros.Promotion.Application.Games.GetGameDetailQuery(id), ct);
        var result = await _mediator.Send(req.ToCommand(id, userId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        await _audit.LogAsync(HttpContext, "Updated", "Game", id, eski.IsSuccess ? eski.Value : null, req, req.FirmPlatformId, req.Code, ct);
        return Ok(new { success = true });
    }

    [HttpDelete("games/{id:guid}")]
    [RequirePermission(Permissions.PromotionManage)]
    public async Task<IActionResult> DeleteGame(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)) return Unauthorized();
        var eski = await _mediator.Send(new ECSPros.Promotion.Application.Games.GetGameDetailQuery(id), ct);
        var result = await _mediator.Send(new ECSPros.Promotion.Application.Games.DeleteGameCommand(id, userId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        await _audit.LogAsync(HttpContext, "Deleted", "Game", id, eski.IsSuccess ? eski.Value : null, null, eski.IsSuccess ? eski.Value!.FirmPlatformId : Guid.Empty, eski.IsSuccess ? eski.Value!.Code : null, ct);
        return Ok(new { success = true });
    }

    /// <summary>Kampanya tipleri (P3 — oluşturma formunun tip seçicisi).</summary>
    /// <summary>Kampanya tiplerini TAM liste olarak (ayar şemasıyla) döner — kampanya listesi ve
    /// kampanya detayı bunu bekler. ⚠ Sayfalanmaz; liste EKRANI için /campaign-types/grid kullanın.</summary>
    [HttpGet("campaign-types")]
    public async Task<IActionResult> GetCampaignTypes([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCampaignTypesQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kampanya tipleri liste ekranı (DataGrid): sayfalı + f.* filtreleri + sort/dir.</summary>
    [HttpGet("campaign-types/grid")]
    public async Task<IActionResult> GetCampaignTypesGrid(
        [FromQuery] bool activeOnly = false, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        // Kampanya TİPİ kanaldan bağımsız bir tanım kaydıdır (kampanyanın kendisi kanala bağlı) → kısıt null.
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null, defaultPageSize: 30);
        var result = await _mediator.Send(new ECSPros.Promotion.Application.Queries.GetCampaignTypes.GetCampaignTypesGridQuery(
            new ECSPros.Promotion.Application.Queries.GetCampaignTypes.CampaignTypeFilters(activeOnly, search),
            grid.Page, grid.PageSize, grid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kampanya tiplerini Excel'e aktarır (DataGrid).</summary>
    [HttpPost("campaign-types/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportCampaignTypes(
        [FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<PromotionController> logger, CancellationToken ct)
    {
        var filters = new ECSPros.Promotion.Application.Queries.GetCampaignTypes.CampaignTypeFilters(
            body.NamedValue("activeOnly") == "true", body.Search);
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "campaign-types", "kampanya-tipleri", "Kampanya Tipleri",
            ECSPros.Api.Grid.CampaignTypeExportColumns.All,
            max => _mediator.Send(new ECSPros.Promotion.Application.Queries.GetCampaignTypes.ExportCampaignTypesQuery(
                filters, body.ToGridRequest(null), max), ct),
            ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    // ─── P3: kupon yönetimi ────────────────────────────────────────────────────

    /// <summary>Kuponları sayfalı listeler (P3; 2026-09-09 DataGrid: f.* filtreleri + sort/dir).</summary>
    [HttpGet("coupons")]
    public async Task<IActionResult> GetCoupons(
        [FromQuery] string? search, [FromQuery] bool? isActive,
        [FromQuery] Guid? memberId = null, [FromQuery] Guid? memberGroupId = null,
        CancellationToken ct = default)
    {
        // Kupon kanaldan bağımsızdır (hedef kuralı üye/grup üzerinden işler) → kanal kısıtı null.
        var grid = ECSPros.Api.Grid.GridRequestParser.Parse(Request.Query, null, defaultPageSize: 20);
        var result = await _mediator.Send(
            new GetCouponsQuery(search, isActive, grid.Page, grid.PageSize, memberId, memberGroupId, grid), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kuponları Excel'e aktarır (DataGrid): gövde search/sort/dir/filters/columns + named: isActive.</summary>
    [HttpPost("coupons/export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("grid-export")]
    public async Task<IActionResult> ExportCoupons(
        [FromServices] ECSPros.Api.Authorization.IAlanYetkileri alanYetkileri, [FromBody] GridExportRequest body,
        [FromServices] IConfiguration config, [FromServices] ECSPros.Iam.Application.Services.IIamDbContext iam,
        [FromServices] ILogger<PromotionController> logger, CancellationToken ct)
    {
        var aktif = body.NamedValue("isActive");
        var filters = new ECSPros.Promotion.Application.Queries.GetCoupons.CouponListFilters(
            body.Search, string.IsNullOrWhiteSpace(aktif) ? null : aktif == "true");
        return await ECSPros.Api.Grid.GridExportEndpoint.RunAsync(this, body, config, iam, logger, "coupons", "kuponlar", "Kuponlar",
            ECSPros.Api.Grid.CouponExportColumns.All,
            max => _mediator.Send(new ECSPros.Promotion.Application.Queries.GetCoupons.ExportCouponsQuery(
                filters, body.ToGridRequest(null), max), ct),
            ct, alanIzinleri: await alanYetkileri.IzinlerAsync(ct));
    }

    /// <summary>Yeni kupon tanımlar (P3).</summary>
    [HttpPost("coupons")]
    [RequirePermission(Permissions.PromotionManage)]   // Y2
    public async Task<IActionResult> CreateCoupon([FromBody] CouponRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)) return Unauthorized();
        var result = await _mediator.Send(new CreateCouponCommand(
            request.Code, request.NameI18n, request.CouponType, request.DiscountValue,
            request.UsageLimitTotal, request.UsageLimitPerMember, request.MinimumCartTotal,
            request.ValidForFirstOrderOnly, AsUtc(request.StartsAt), AsUtcNullable(request.EndsAt),
            request.MemberId, request.MemberGroupId, userId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created("/api/promotion/coupons", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Kupon günceller (P3).</summary>
    [HttpPut("coupons/{id:guid}")]
    [RequirePermission(Permissions.PromotionManage)]   // Y2
    public async Task<IActionResult> UpdateCoupon(Guid id, [FromBody] CouponRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)) return Unauthorized();
        var result = await _mediator.Send(new UpdateCouponCommand(
            id, request.NameI18n, request.CouponType, request.DiscountValue,
            request.UsageLimitTotal, request.UsageLimitPerMember, request.MinimumCartTotal,
            request.ValidForFirstOrderOnly, AsUtc(request.StartsAt), AsUtcNullable(request.EndsAt),
            request.MemberId, request.MemberGroupId, request.IsActive, userId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Kuponu siler — yalnız hiç kullanılmamış kuponlar silinebilir (P3).</summary>
    [HttpDelete("coupons/{id:guid}")]
    [RequirePermission(Permissions.PromotionManage)]   // Y2
    public async Task<IActionResult> DeleteCoupon(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)) return Unauthorized();
        var result = await _mediator.Send(new DeleteCouponCommand(id, userId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Kuponun kullanım kayıtları (P3).</summary>
    [HttpGet("coupons/{id:guid}/usages")]
    public async Task<IActionResult> GetCouponUsages(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCouponUsagesQuery(id, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    private static DateTime AsUtc(DateTime d) =>
        d.Kind == DateTimeKind.Utc ? d : d.ToUniversalTime();
    private static DateTime? AsUtcNullable(DateTime? d) => d is null ? null : AsUtc(d.Value);

    /// <summary>
    /// Sepet için uygulanabilir kampanya indirimlerini hesaplar.
    /// Sipariş oluşturmadan önce çağrılır — sonuç gösterimi ve doğrulama için.
    /// </summary>
    [HttpPost("calculate")]
    [RequirePermission(Permissions.PromotionManage)]   // Y2
    public async Task<IActionResult> CalculateDiscounts([FromBody] CalculateDiscountsRequest request, CancellationToken ct)
    {
        var items = request.Items
            .Select(i => new CartLineItem(i.VariantId, i.Quantity, i.UnitPrice))
            .ToList();

        var result = await _mediator.Send(new CalculateDiscountsQuery(items, request.MemberId), ct);

        return Ok(new
        {
            success = true,
            data = new
            {
                discounts = result.Value,
                totalDiscount = result.Value.Sum(d => d.DiscountAmount)
            }
        });
    }

    /// <summary>Kupon kodunu doğrular ve indirim tutarını hesaplar.</summary>
    [HttpPost("coupon/validate")]
    [RequirePermission(Permissions.PromotionManage)]   // Y2
    public async Task<IActionResult> ValidateCoupon([FromBody] ValidateCouponRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ValidateCouponQuery(request.Code, request.CartTotal, request.MemberId, request.IsFirstOrder), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Kupon kullanımını kaydeder — sipariş tamamlandıktan sonra çağrılır.</summary>
    [HttpPost("coupon/use")]
    [RequirePermission(Permissions.PromotionManage)]   // Y2
    public async Task<IActionResult> UseCoupon([FromBody] UseCouponRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out _))
            return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(
            new UseCouponCommand(request.CouponId, request.MemberId, request.OrderId, request.DiscountAmount), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }
}

// ─── Request records ────────────────────────────────────────────────────────

public record CreateCampaignRequest(
    Guid FirmPlatformId,
    Guid CampaignTypeId,
    string? Code,
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? DescriptionI18n,
    string? BadgeLabel,
    string? BadgeColor,
    DateTime StartsAt,
    DateTime? EndsAt,
    int Priority,
    bool IsActive,
    Dictionary<string, object>? Settings,
    string FillType,
    Dictionary<string, object>? FilterDef,
    List<Guid>? ManualProductIds,
    List<Guid>? ExcludedProductIds);

public record UpdateCampaignRequest(
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? DescriptionI18n,
    string? BadgeLabel,
    string? BadgeColor,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive,
    int Priority,
    Dictionary<string, object>? Settings,
    string FillType,
    Dictionary<string, object>? FilterDef,
    List<Guid>? ManualProductIds,
    List<Guid>? ExcludedProductIds);

public record CopyCampaignRequest(string NewCode, Guid? TargetFirmPlatformId);

public record CouponRequest(
    string Code,
    Dictionary<string, string> NameI18n,
    string CouponType,
    decimal DiscountValue,
    int? UsageLimitTotal,
    int? UsageLimitPerMember,
    decimal? MinimumCartTotal,
    bool ValidForFirstOrderOnly,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive = true,
    Guid? MemberId = null,          // kişiye özel kupon
    Guid? MemberGroupId = null);    // üye grubuna özel kupon (ikisi birden olamaz)

public record CartItemRequest(Guid VariantId, decimal Quantity, decimal UnitPrice);

public record CalculateDiscountsRequest(
    List<CartItemRequest> Items,
    Guid? MemberId = null);

public record ValidateCouponRequest(
    string Code,
    decimal CartTotal,
    Guid? MemberId = null,
    bool IsFirstOrder = false);

public record UseCouponRequest(
    Guid CouponId,
    Guid MemberId,
    Guid OrderId,
    decimal DiscountAmount);

/// <summary>Şans oyunu kaydı (panel formu) — alan adları GameDetailDto ile aynı.</summary>
public record SaveGameRequest(
    Guid FirmPlatformId, string Code, string Type,
    Dictionary<string, string> TitleI18n, Dictionary<string, string>? SubtitleI18n, Dictionary<string, string>? DescriptionI18n,
    Dictionary<string, string>? RulesTextI18n, string? CtaLabel, string? ImageUrl, string? ThemeColor, string? AccentColor,
    bool AlwaysWin, DateTime StartsAt, DateTime? EndsAt, bool IsActive, string LimitPeriod, int LimitCount, int CouponValidDays, int SortOrder,
    string? LabelAvailable, string? LabelCooldown, string? LabelExhausted, string? LabelLoginRequired, string? LabelEnded,
    string? WinMessage, string? WinSubMessage, string? LoseMessage, string? LoseSubMessage,
    List<ECSPros.Promotion.Application.Games.GamePrizeDto> Prizes)
{
    public ECSPros.Promotion.Application.Games.SaveGameCommand ToCommand(Guid? id, Guid userId) => new(
        id, FirmPlatformId, Code, Type, TitleI18n, SubtitleI18n, DescriptionI18n, RulesTextI18n, CtaLabel, ImageUrl, ThemeColor, AccentColor,
        AlwaysWin, StartsAt, EndsAt, IsActive, LimitPeriod, Math.Max(1, LimitCount), Math.Max(1, CouponValidDays), SortOrder,
        LabelAvailable, LabelCooldown, LabelExhausted, LabelLoginRequired, LabelEnded, WinMessage, WinSubMessage, LoseMessage, LoseSubMessage,
        Prizes ?? new(), userId);
}
