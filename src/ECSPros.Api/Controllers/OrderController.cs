using ECSPros.Order.Application.Commands.ApproveReturn;
using ECSPros.Order.Application.Commands.CancelInvoice;
using ECSPros.Order.Application.Commands.CancelOrder;
using ECSPros.Order.Application.Commands.CompleteRefund;
using ECSPros.Order.Application.Commands.ConfirmOrder;
using ECSPros.Order.Application.Commands.ConvertQuoteToOrder;
using ECSPros.Order.Application.Commands.CreateGiftCard;
using ECSPros.Order.Application.Commands.CreateInvoice;
using ECSPros.Order.Application.Commands.CreateOrder;
using ECSPros.Order.Application.Commands.CreateQuote;
using ECSPros.Order.Application.Commands.CreateReturn;
using ECSPros.Order.Application.Commands.MarkDelivered;
using ECSPros.Order.Application.Commands.MarkShipped;
using ECSPros.Order.Application.Commands.ReceiveReturn;
using ECSPros.Order.Application.Commands.RespondQuote;
using ECSPros.Order.Application.Commands.SendQuote;
using ECSPros.Order.Application.Commands.StartProcessing;
using ECSPros.Order.Application.Commands.UseGiftCard;
using ECSPros.Order.Application.Queries.GetGiftCardBalance;
using ECSPros.Order.Application.Queries.GetInvoices;
using ECSPros.Order.Application.Queries.GetOrderDetail;
using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Order.Application.Queries.GetQuotes;
using ECSPros.Order.Application.Queries.GetReturnDetail;
using ECSPros.Order.Application.Queries.GetReturns;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

    // ─── Orders ───────────────────────────────────────────────────────────────

    /// <summary>Siparişleri sayfalı listeler.</summary>
    [HttpGet]
    public async Task<IActionResult> GetOrders(
        [FromQuery] string? status, [FromQuery] Guid? memberId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetOrdersQuery(status, memberId, search, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Sipariş detayını döner.</summary>
    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> GetOrderDetail(Guid orderId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOrderDetailQuery(orderId), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni sipariş oluşturur.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var items = request.Items.Select(i => new OrderItemDto(i.VariantId, i.Quantity, i.UnitPrice, i.UnitType)).ToList();
        var result = await _mediator.Send(new CreateOrderCommand(
            request.FirmPlatformId, request.MemberId, request.OrderType, request.PaymentMethod,
            request.CurrencyCode, request.ShippingRecipientName, request.ShippingRecipientPhone,
            request.ShippingCountryId, request.ShippingCityId, request.ShippingDistrictId,
            request.ShippingAddressLine, request.ShippingPostalCode, items, request.CustomerNotes), ct);

        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/orders/{result.Value}", new { success = true, data = new { orderNumber = result.Value } });
    }

    /// <summary>Siparişi onaylar ve stok rezervasyonu oluşturur.</summary>
    [HttpPost("{orderId:guid}/confirm")]
    public async Task<IActionResult> ConfirmOrder(Guid orderId, [FromBody] ConfirmOrderRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new ConfirmOrderCommand(orderId, request.WarehouseId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Siparişi iptal eder ve stok rezervasyonlarını serbest bırakır.</summary>
    [HttpPost("{orderId:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid orderId, [FromBody] CancelOrderRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new CancelOrderCommand(orderId, uid, request.Reason), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Siparişi işleme alır (picking planı atanabilir).</summary>
    [HttpPost("{orderId:guid}/start-processing")]
    public async Task<IActionResult> StartProcessing(Guid orderId, [FromBody] StartProcessingRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new StartProcessingCommand(orderId, request.PickingPlanId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Siparişi kargoya verir, Shipment kaydı oluşturur, stok rezervasyonunu tüketir.</summary>
    [HttpPost("{orderId:guid}/ship")]
    public async Task<IActionResult> MarkShipped(Guid orderId, [FromBody] MarkShippedRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new MarkShippedCommand(orderId, request.FirmIntegrationId, request.TrackingNumber, request.PackageCount, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { shipmentId = result.Value } });
    }

    /// <summary>Siparişi teslim edildi olarak işaretler.</summary>
    [HttpPost("{orderId:guid}/deliver")]
    public async Task<IActionResult> MarkDelivered(Guid orderId, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new MarkDeliveredCommand(orderId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── Returns ──────────────────────────────────────────────────────────────

    /// <summary>İade taleplerini listeler.</summary>
    [HttpGet("returns")]
    public async Task<IActionResult> GetReturns(
        [FromQuery] Guid? orderId, [FromQuery] Guid? memberId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetReturnsQuery(orderId, memberId, status, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>İade talebi detayını döner.</summary>
    [HttpGet("returns/{returnId:guid}")]
    public async Task<IActionResult> GetReturnDetail(Guid returnId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetReturnDetailQuery(returnId), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Müşteri iade talebi oluşturur.</summary>
    [HttpPost("{orderId:guid}/returns")]
    public async Task<IActionResult> CreateReturn(Guid orderId, [FromBody] CreateReturnRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateReturnCommand(
            orderId, request.MemberId, request.ReturnType, request.CustomerNotes, request.RefundMethod,
            request.Items.Select(i => new ReturnItemRequest(i.OrderItemId, i.VariantId, i.Quantity, i.ReturnReasonId, i.CustomerNotes)).ToList()), ct);

        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/orders/returns/{result.Value}", new { success = true, data = new { returnId = result.Value } });
    }

    /// <summary>İade talebini onaylar.</summary>
    [HttpPost("returns/{returnId:guid}/approve")]
    public async Task<IActionResult> ApproveReturn(Guid returnId, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new ApproveReturnCommand(returnId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>İade kargosu depoda teslim alındı — stok otomatik geri yüklenir.</summary>
    [HttpPost("returns/{returnId:guid}/receive")]
    public async Task<IActionResult> ReceiveReturn(Guid returnId, [FromBody] ReceiveReturnRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new ReceiveReturnCommand(returnId, request.WarehouseId, request.InspectionNotes, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>İade bedelini öder — iade tamamlanır.</summary>
    [HttpPost("returns/{returnId:guid}/refund")]
    public async Task<IActionResult> CompleteRefund(Guid returnId, [FromBody] CompleteRefundRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new CompleteRefundCommand(returnId, request.RefundMethod, request.Amount, uid, request.Details), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── Invoices ─────────────────────────────────────────────────────────────

    /// <summary>Fatura listesi.</summary>
    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] Guid? orderId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetInvoicesQuery(orderId, status, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Sipariş için fatura oluşturur.</summary>
    [HttpPost("{orderId:guid}/invoices")]
    public async Task<IActionResult> CreateInvoice(Guid orderId, [FromBody] CreateInvoiceRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new CreateInvoiceCommand(
            orderId, request.InvoiceSeriesId, request.InvoiceType, request.InvoiceDate,
            request.RecipientName, request.RecipientAddress,
            request.RecipientTaxOffice, request.RecipientTaxNumber, request.RecipientCompanyName, uid), ct);

        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/orders/invoices/{result.Value}", new { success = true, data = new { invoiceId = result.Value } });
    }

    /// <summary>Fatura iptali.</summary>
    [HttpPost("invoices/{invoiceId:guid}/cancel")]
    public async Task<IActionResult> CancelInvoice(Guid invoiceId, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new CancelInvoiceCommand(invoiceId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ─── Quotes ───────────────────────────────────────────────────────────────

    /// <summary>Teklif listesi.</summary>
    [HttpGet("quotes")]
    public async Task<IActionResult> GetQuotes(
        [FromQuery] Guid? memberId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetQuotesQuery(memberId, status, page, pageSize), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni teklif oluşturur.</summary>
    [HttpPost("quotes")]
    public async Task<IActionResult> CreateQuote([FromBody] CreateQuoteRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new CreateQuoteCommand(
            request.FirmPlatformId, request.MemberId, request.CurrencyCode, request.ValidUntil,
            request.NotesToCustomer, request.InternalNotes,
            request.Items.Select(i => new QuoteItemRequest(
                i.VariantId, i.Sku, i.ProductName, i.VariantInfo, i.Quantity,
                i.UnitType, i.UnitPrice, i.DiscountRate, i.TaxRate, i.Notes)).ToList(), uid), ct);

        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/orders/quotes/{result.Value}", new { success = true, data = new { quoteId = result.Value } });
    }

    /// <summary>Teklifi müşteriye gönderir.</summary>
    [HttpPost("quotes/{quoteId:guid}/send")]
    public async Task<IActionResult> SendQuote(Guid quoteId, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new SendQuoteCommand(quoteId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Teklifi kabul veya reddeder.</summary>
    [HttpPost("quotes/{quoteId:guid}/respond")]
    public async Task<IActionResult> RespondQuote(Guid quoteId, [FromBody] RespondQuoteRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new RespondQuoteCommand(quoteId, request.Accepted, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    /// <summary>Kabul edilen teklifi siparişe dönüştürür.</summary>
    [HttpPost("quotes/{quoteId:guid}/convert")]
    public async Task<IActionResult> ConvertQuoteToOrder(Guid quoteId, [FromBody] ConvertQuoteRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new ConvertQuoteToOrderCommand(
            quoteId, request.ShippingRecipientName, request.ShippingRecipientPhone,
            request.ShippingCountryId, request.ShippingCityId, request.ShippingDistrictId,
            request.ShippingAddressLine, request.ShippingPostalCode, request.PaymentMethodId, uid), ct);

        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/orders/{result.Value}", new { success = true, data = new { orderId = result.Value } });
    }

    // ─── Gift Cards ───────────────────────────────────────────────────────────

    /// <summary>Hediye kartı bakiyesi sorgular.</summary>
    [HttpGet("gift-cards/{code}")]
    public async Task<IActionResult> GetGiftCardBalance(string code, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetGiftCardBalanceQuery(code), ct);
        if (result.IsFailure) return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni hediye kartı oluşturur.</summary>
    [HttpPost("gift-cards")]
    public async Task<IActionResult> CreateGiftCard([FromBody] CreateGiftCardRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new CreateGiftCardCommand(
            request.FirmId, request.Amount, request.CurrencyCode,
            request.ValidFrom, request.ValidUntil, request.IsSingleUse,
            request.CreatedForMemberId, request.CreatedFromOrderId, uid), ct);

        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Created($"/api/orders/gift-cards/{result.Value}", new { success = true, data = new { giftCardId = result.Value } });
    }

    /// <summary>Hediye kartını siparişe uygular.</summary>
    [HttpPost("gift-cards/use")]
    public async Task<IActionResult> UseGiftCard([FromBody] UseGiftCardRequest request, CancellationToken ct)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized(new { success = false, error = "Geçersiz token." });

        var result = await _mediator.Send(new UseGiftCardCommand(request.Code, request.Amount, request.OrderId, uid), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = new { deductedAmount = result.Value } });
    }
}

// ─── Request records ──────────────────────────────────────────────────────────

public record CreateOrderRequest(
    Guid FirmPlatformId, Guid MemberId, string OrderType, string PaymentMethod,
    string CurrencyCode, string ShippingRecipientName, string ShippingRecipientPhone,
    Guid ShippingCountryId, Guid ShippingCityId, Guid ShippingDistrictId,
    string ShippingAddressLine, string? ShippingPostalCode,
    List<OrderItemRequest> Items, string? CustomerNotes = null);

public record OrderItemRequest(Guid VariantId, int Quantity, decimal UnitPrice, string? UnitType = null);
public record ConfirmOrderRequest(Guid WarehouseId);
public record CancelOrderRequest(string? Reason = null);
public record StartProcessingRequest(Guid? PickingPlanId = null);
public record MarkShippedRequest(Guid? FirmIntegrationId, string? TrackingNumber, int PackageCount = 1);

public record CreateReturnItemRequest(
    Guid OrderItemId, Guid VariantId, int Quantity, Guid ReturnReasonId, string? CustomerNotes);

public record CreateReturnRequest(
    Guid MemberId, string ReturnType, string? CustomerNotes,
    string RefundMethod, List<CreateReturnItemRequest> Items);

public record ReceiveReturnRequest(Guid WarehouseId, string? InspectionNotes);

public record CompleteRefundRequest(
    string RefundMethod, decimal Amount, Dictionary<string, object>? Details = null);

public record CreateInvoiceRequest(
    Guid InvoiceSeriesId, string InvoiceType, DateTime InvoiceDate,
    string RecipientName, string RecipientAddress,
    string? RecipientTaxOffice, string? RecipientTaxNumber, string? RecipientCompanyName);

public record QuoteItemHttpRequest(
    Guid VariantId, string Sku, string ProductName, string VariantInfo,
    int Quantity, string UnitType, decimal UnitPrice,
    decimal DiscountRate, decimal TaxRate, string? Notes);

public record CreateQuoteRequest(
    Guid FirmPlatformId, Guid MemberId, string CurrencyCode, DateTime ValidUntil,
    string? NotesToCustomer, string? InternalNotes, List<QuoteItemHttpRequest> Items);

public record RespondQuoteRequest(bool Accepted);

public record ConvertQuoteRequest(
    string ShippingRecipientName, string ShippingRecipientPhone,
    Guid ShippingCountryId, Guid ShippingCityId, Guid ShippingDistrictId,
    string ShippingAddressLine, string? ShippingPostalCode, Guid PaymentMethodId);

public record CreateGiftCardRequest(
    Guid FirmId, decimal Amount, string CurrencyCode,
    DateOnly ValidFrom, DateOnly? ValidUntil, bool IsSingleUse,
    Guid? CreatedForMemberId, Guid? CreatedFromOrderId);

public record UseGiftCardRequest(string Code, decimal Amount, Guid OrderId);
