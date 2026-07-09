using ECSPros.Order.Application.Commands.Checkout;
using ECSPros.Promotion.Application.Queries.ValidateCoupon;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/store/checkout")]
[Authorize(Policy = "MemberOnly")]
public class StoreCheckoutController(IMediator mediator) : ControllerBase
{
    /// <summary>C3: sepette kupon kodu doğrulama — misafir de deneyebilir (üye kuponu
    /// koşulları MemberId üzerinden değerlendirilir); kullanım kaydı checkout'ta (C10).</summary>
    [HttpPost("coupon/validate")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateCoupon([FromBody] StoreCouponValidateRequest req, CancellationToken ct)
    {
        Guid? memberId = null;
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (sub != null && Guid.TryParse(sub, out var mid)) memberId = mid;

        var result = await mediator.Send(new ValidateCouponQuery(req.Code, req.CartTotal, memberId), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost]
    public async Task<IActionResult> Checkout([FromBody] StoreCheckoutRequest req, CancellationToken ct)
    {
        var memberId = Guid.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!);

        var result = await mediator.Send(new CheckoutCommand(
            req.FirmPlatformId, memberId, req.CurrencyCode,
            req.ShippingRecipientName, req.ShippingRecipientPhone,
            req.ShippingCountryId, req.ShippingCityId, req.ShippingDistrictId,
            req.ShippingAddressLine, req.ShippingPostalCode, req.ShippingDeliveryNotes,
            req.BillingSameAsShipping, req.BillingRecipientName,
            req.BillingTaxOffice, req.BillingTaxNumber, req.BillingCompanyName,
            req.BillingCountryId, req.BillingCityId, req.BillingDistrictId, req.BillingAddressLine,
            req.Items.Select(i => new CheckoutItem(i.VariantId, i.Sku, i.ProductName, i.VariantInfo, i.Quantity, i.UnitPrice)).ToList(),
            req.CustomerNotes, req.CartId), ct);

        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });

        // C10: kupon kullanım kaydı (C3'te yalnız doğrulanmıştı) — sipariş oluştuktan sonra
        if (req.CouponId is { } kuponId && req.CouponDiscount is { } indirim)
            await mediator.Send(new ECSPros.Promotion.Application.Commands.UseCoupon.UseCouponCommand(
                kuponId, memberId, result.Value, indirim), ct);

        return Ok(new { success = true, data = new { orderId = result.Value } });
    }
}

public record StoreCheckoutRequest(
    Guid FirmPlatformId,
    string CurrencyCode,
    string ShippingRecipientName,
    string ShippingRecipientPhone,
    Guid ShippingCountryId,
    Guid ShippingCityId,
    Guid ShippingDistrictId,
    string ShippingAddressLine,
    string? ShippingPostalCode,
    string? ShippingDeliveryNotes,
    bool BillingSameAsShipping,
    string? BillingRecipientName,
    string? BillingTaxOffice,
    string? BillingTaxNumber,
    string? BillingCompanyName,
    Guid? BillingCountryId,
    Guid? BillingCityId,
    Guid? BillingDistrictId,
    string? BillingAddressLine,
    List<StoreCheckoutItemRequest> Items,
    string? CustomerNotes = null,
    Guid? CartId = null,
    Guid? CouponId = null,           // C10: uygulanan kuponun kullanım kaydı için
    decimal? CouponDiscount = null);

public record StoreCheckoutItemRequest(
    Guid VariantId,
    string Sku,
    string ProductName,
    string VariantInfo,
    int Quantity,
    decimal UnitPrice);

public record StoreCouponValidateRequest(string Code, decimal CartTotal);
