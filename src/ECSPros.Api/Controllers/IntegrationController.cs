using ECSPros.Integration.Application.Adapters;
using ECSPros.Integration.Application.Commands.CreateCargoShipment;
using ECSPros.Integration.Application.Commands.FetchMarketplaceOrders;
using ECSPros.Integration.Application.Commands.SendEInvoice;
using ECSPros.Integration.Application.Commands.SyncMarketplaceProduct;
using ECSPros.Integration.Application.Commands.TrackCargoShipment;
using ECSPros.Integration.Application.Commands.UpdateMarketplaceStock;
using ECSPros.Integration.Application.Queries.GetIntegrationLogs;
using ECSPros.Integration.Application.Queries.GetMarketplaceProducts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/integrations")]
[Authorize]
public class IntegrationController(IMediator mediator) : ControllerBase
{
    // ─── Logs ───────────────────────────────────────────────────────────
    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] Guid? firmIntegrationId,
        [FromQuery] string? serviceType,
        [FromQuery] string? operationType,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetIntegrationLogsQuery(
            firmIntegrationId, serviceType, operationType, status, from, to, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // ─── Marketplace ────────────────────────────────────────────────────
    [HttpGet("marketplace/products")]
    public async Task<IActionResult> GetMarketplaceProducts(
        [FromQuery] Guid firmIntegrationId,
        [FromQuery] string? syncStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetMarketplaceProductsQuery(firmIntegrationId, syncStatus, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("marketplace/sync-product")]
    public async Task<IActionResult> SyncProduct(
        [FromBody] SyncProductRequest request,
        CancellationToken ct = default)
    {
        var payload = new MarketplaceProductPayload(
            request.VariantId,
            request.Barcode,
            request.Title,
            request.Description,
            request.Price,
            request.StockQuantity,
            request.Attributes);

        var result = await mediator.Send(new SyncMarketplaceProductCommand(
            request.FirmIntegrationId, request.ServiceCode, payload), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("marketplace/update-stock")]
    public async Task<IActionResult> UpdateStock(
        [FromBody] UpdateStockRequest request,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new UpdateMarketplaceStockCommand(
            request.FirmIntegrationId, request.ServiceCode, request.VariantId, request.Quantity), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("marketplace/fetch-orders")]
    public async Task<IActionResult> FetchOrders(
        [FromBody] FetchOrdersRequest request,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new FetchMarketplaceOrdersCommand(
            request.FirmIntegrationId, request.ServiceCode, request.Since), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // ─── Cargo ──────────────────────────────────────────────────────────
    [HttpPost("cargo/shipments")]
    public async Task<IActionResult> CreateShipment(
        [FromBody] CreateShipmentRequest request,
        CancellationToken ct = default)
    {
        var shipmentRequest = new CargoShipmentRequest(
            request.OrderId,
            request.RecipientName,
            request.RecipientPhone,
            request.AddressLine,
            request.CityCode,
            request.DistrictCode,
            request.PackageCount,
            request.TotalWeight,
            request.TotalDesi,
            request.IsCod,
            request.CodAmount);

        var result = await mediator.Send(new CreateCargoShipmentCommand(
            request.FirmIntegrationId, request.ServiceCode, shipmentRequest), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpPost("cargo/shipments/{trackingNumber}/track")]
    public async Task<IActionResult> TrackShipment(
        string trackingNumber,
        [FromQuery] Guid firmIntegrationId,
        [FromQuery] string serviceCode,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new TrackCargoShipmentCommand(
            firmIntegrationId, serviceCode, trackingNumber), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    // ─── e-Invoice ──────────────────────────────────────────────────────
    [HttpPost("einvoice/send")]
    public async Task<IActionResult> SendEInvoice(
        [FromBody] SendEInvoiceRequest request,
        CancellationToken ct = default)
    {
        var payload = new EInvoicePayload(
            request.OrderId,
            request.InvoiceNumber,
            request.RecipientName,
            request.RecipientTaxId,
            request.RecipientAddress,
            request.TotalAmount,
            request.TaxAmount,
            request.CurrencyCode,
            request.InvoiceDate,
            request.Lines.Select(l => new EInvoiceLineDto(
                l.Description, l.Quantity, l.UnitPrice, l.TaxRate, l.LineTotal)).ToList());

        var result = await mediator.Send(new SendEInvoiceCommand(
            request.FirmIntegrationId, request.ServiceCode, payload), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}

// ─── Request Records ────────────────────────────────────────────────────────
public record SyncProductRequest(
    Guid FirmIntegrationId,
    string ServiceCode,
    Guid VariantId,
    string Barcode,
    string Title,
    string Description,
    decimal Price,
    int StockQuantity,
    Dictionary<string, string>? Attributes = null);

public record UpdateStockRequest(
    Guid FirmIntegrationId,
    string ServiceCode,
    Guid VariantId,
    int Quantity);

public record FetchOrdersRequest(
    Guid FirmIntegrationId,
    string ServiceCode,
    DateTime? Since = null);

public record CreateShipmentRequest(
    Guid FirmIntegrationId,
    string ServiceCode,
    Guid OrderId,
    string RecipientName,
    string RecipientPhone,
    string AddressLine,
    string CityCode,
    string DistrictCode,
    int PackageCount,
    decimal? TotalWeight,
    decimal? TotalDesi,
    bool IsCod = false,
    decimal CodAmount = 0);

public record SendEInvoiceRequest(
    Guid FirmIntegrationId,
    string ServiceCode,
    Guid OrderId,
    string InvoiceNumber,
    string RecipientName,
    string RecipientTaxId,
    string RecipientAddress,
    decimal TotalAmount,
    decimal TaxAmount,
    string CurrencyCode,
    DateTime InvoiceDate,
    List<EInvoiceLineRequest> Lines);

public record EInvoiceLineRequest(
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal LineTotal);
