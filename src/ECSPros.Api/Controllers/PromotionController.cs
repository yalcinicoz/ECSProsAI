using ECSPros.Promotion.Application.Commands.CreateCampaign;
using ECSPros.Promotion.Application.Commands.ManageCoupon;
using ECSPros.Promotion.Application.Commands.UpdateCampaign;
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
public class PromotionController : ControllerBase
{
    private readonly IMediator _mediator;

    public PromotionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Kampanyaları listeler.</summary>
    [HttpGet("campaigns")]
    public async Task<IActionResult> GetCampaigns(
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCampaignsQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni kampanya oluşturur.</summary>
    [HttpPost("campaigns")]
    public async Task<IActionResult> CreateCampaign([FromBody] CreateCampaignRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateCampaignCommand(
            request.CampaignTypeId,
            request.Code,
            request.NameI18n,
            request.StartsAt,
            request.EndsAt,
            request.Priority,
            request.ProductSelectionType,
            request.Settings ?? new Dictionary<string, object>()), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/promotion/campaigns", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Kampanya günceller.</summary>
    [HttpPut("campaigns/{id:guid}")]
    public async Task<IActionResult> UpdateCampaign(Guid id, [FromBody] UpdateCampaignRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await _mediator.Send(new UpdateCampaignCommand(
            id,
            request.NameI18n,
            request.DescriptionI18n,
            request.StartsAt,
            request.EndsAt,
            request.IsActive,
            request.Priority,
            userId,
            request.Settings), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

    /// <summary>Kampanya tipleri (P3 — oluşturma formunun tip seçicisi).</summary>
    [HttpGet("campaign-types")]
    public async Task<IActionResult> GetCampaignTypes([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCampaignTypesQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    // ─── P3: kupon yönetimi ────────────────────────────────────────────────────

    /// <summary>Kuponları sayfalı listeler (P3).</summary>
    [HttpGet("coupons")]
    public async Task<IActionResult> GetCoupons(
        [FromQuery] string? search, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCouponsQuery(search, isActive, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni kupon tanımlar (P3).</summary>
    [HttpPost("coupons")]
    public async Task<IActionResult> CreateCoupon([FromBody] CouponRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)) return Unauthorized();
        var result = await _mediator.Send(new CreateCouponCommand(
            request.Code, request.NameI18n, request.CouponType, request.DiscountValue,
            request.UsageLimitTotal, request.UsageLimitPerMember, request.MinimumCartTotal,
            request.ValidForFirstOrderOnly, AsUtc(request.StartsAt), AsUtcNullable(request.EndsAt), userId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created("/api/promotion/coupons", new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Kupon günceller (P3).</summary>
    [HttpPut("coupons/{id:guid}")]
    public async Task<IActionResult> UpdateCoupon(Guid id, [FromBody] CouponRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)) return Unauthorized();
        var result = await _mediator.Send(new UpdateCouponCommand(
            id, request.NameI18n, request.CouponType, request.DiscountValue,
            request.UsageLimitTotal, request.UsageLimitPerMember, request.MinimumCartTotal,
            request.ValidForFirstOrderOnly, AsUtc(request.StartsAt), AsUtcNullable(request.EndsAt),
            request.IsActive, userId), ct);
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
    Guid CampaignTypeId,
    string Code,
    Dictionary<string, string> NameI18n,
    DateTime StartsAt,
    DateTime? EndsAt,
    int Priority,
    string ProductSelectionType,
    Dictionary<string, object>? Settings);

public record UpdateCampaignRequest(
    Dictionary<string, string> NameI18n,
    Dictionary<string, string>? DescriptionI18n,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive,
    int Priority,
    Dictionary<string, object>? Settings = null);

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
    bool IsActive = true);

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
