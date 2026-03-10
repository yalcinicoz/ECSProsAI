using ECSPros.Order.Application.Commands.Checkout;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/store/checkout")]
[Authorize(Policy = "MemberOnly")]
public class StoreCheckoutController(IMediator mediator) : ControllerBase
{
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
    Guid? CartId = null);

public record StoreCheckoutItemRequest(
    Guid VariantId,
    string Sku,
    string ProductName,
    string VariantInfo,
    int Quantity,
    decimal UnitPrice);
