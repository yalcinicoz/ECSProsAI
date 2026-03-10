using ECSPros.Promotion.Application.Commands.CreateCampaign;
using ECSPros.Promotion.Application.Commands.UpdateCampaign;
using ECSPros.Promotion.Application.Commands.UseCoupon;
using ECSPros.Promotion.Application.Queries.CalculateDiscounts;
using ECSPros.Promotion.Application.Queries.GetCampaigns;
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
            userId), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Ok(new { success = true });
    }

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
    int Priority);

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
