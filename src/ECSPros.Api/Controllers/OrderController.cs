using ECSPros.Order.Application.Commands.CreateOrder;
using ECSPros.Order.Application.Queries.GetOrders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrderController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Siparişleri sayfalı listeler.</summary>
    [HttpGet]
    public async Task<IActionResult> GetOrders(
        [FromQuery] string? status,
        [FromQuery] Guid? memberId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetOrdersQuery(status, memberId, search, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni sipariş oluşturur.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var items = request.Items
            .Select(i => new OrderItemDto(i.VariantId, i.Quantity, i.UnitPrice, i.UnitType))
            .ToList();

        var result = await _mediator.Send(new CreateOrderCommand(
            request.FirmPlatformId,
            request.MemberId,
            request.OrderType,
            request.PaymentMethod,
            request.CurrencyCode,
            request.ShippingRecipientName,
            request.ShippingRecipientPhone,
            request.ShippingCountryId,
            request.ShippingCityId,
            request.ShippingDistrictId,
            request.ShippingAddressLine,
            request.ShippingPostalCode,
            items,
            request.CustomerNotes), ct);

        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });

        return Created($"/api/orders/{result.Value}", new { success = true, data = new { orderNumber = result.Value } });
    }
}

public record CreateOrderRequest(
    Guid FirmPlatformId,
    Guid MemberId,
    string OrderType,
    string PaymentMethod,
    string CurrencyCode,
    string ShippingRecipientName,
    string ShippingRecipientPhone,
    Guid ShippingCountryId,
    Guid ShippingCityId,
    Guid ShippingDistrictId,
    string ShippingAddressLine,
    string? ShippingPostalCode,
    List<OrderItemRequest> Items,
    string? CustomerNotes = null);

public record OrderItemRequest(Guid VariantId, int Quantity, decimal UnitPrice, string? UnitType = null);
