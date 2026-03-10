using ECSPros.Core.Application.Commands.CreateCargoRule;
using ECSPros.Core.Application.Commands.CreateExpenseType;
using ECSPros.Core.Application.Commands.CreateFirm;
using ECSPros.Core.Application.Commands.CreateFirmIntegration;
using ECSPros.Core.Application.Commands.CreateFirmPlatform;
using ECSPros.Core.Application.Commands.UpdateFirm;
using ECSPros.Core.Application.Commands.UpdateFirmPlatform;
using ECSPros.Core.Application.Queries.GetCargoRules;
using ECSPros.Core.Application.Queries.GetExpenseTypes;
using ECSPros.Core.Application.Queries.GetFirmDetail;
using ECSPros.Core.Application.Queries.GetFirmIntegrations;
using ECSPros.Core.Application.Queries.GetFirmPlatforms;
using ECSPros.Core.Application.Queries.GetFirms;
using ECSPros.Core.Application.Queries.GetIntegrationServices;
using ECSPros.Core.Application.Queries.GetLanguages;
using ECSPros.Core.Application.Queries.GetOrderStatuses;
using ECSPros.Core.Application.Queries.GetPaymentMethods;
using ECSPros.Core.Application.Queries.GetPlatformTypes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

[ApiController]
[Route("api/core")]
[Authorize]
public class CoreController : ControllerBase
{
    private readonly IMediator _mediator;

    public CoreController(IMediator mediator) => _mediator = mediator;

    // ── Diller ────────────────────────────────────────────────────────────────

    /// <summary>Aktif dilleri listeler.</summary>
    [HttpGet("languages")]
    public async Task<IActionResult> GetLanguages([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetLanguagesQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    // ── Referans Veriler ───────────────────────────────────────────────────────

    /// <summary>Sipariş durumlarını listeler.</summary>
    [HttpGet("order-statuses")]
    public async Task<IActionResult> GetOrderStatuses([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetOrderStatusesQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Ödeme yöntemlerini listeler.</summary>
    [HttpGet("payment-methods")]
    public async Task<IActionResult> GetPaymentMethods([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPaymentMethodsQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Platform tiplerini listeler (trendyol, hepsiburada, site vb.).</summary>
    [HttpGet("platform-types")]
    public async Task<IActionResult> GetPlatformTypes([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPlatformTypesQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Entegrasyon servislerini listeler.</summary>
    [HttpGet("integration-services")]
    public async Task<IActionResult> GetIntegrationServices([FromQuery] string? serviceType = null, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetIntegrationServicesQuery(serviceType), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Masraf tiplerini listeler.</summary>
    [HttpGet("expense-types")]
    public async Task<IActionResult> GetExpenseTypes([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetExpenseTypesQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni masraf tipi oluşturur.</summary>
    [HttpPost("expense-types")]
    public async Task<IActionResult> CreateExpenseType([FromBody] CreateExpenseTypeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateExpenseTypeCommand(request.Code, request.NameI18n, request.IsItemLevel, request.DefaultTaxRate, request.SortOrder), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    // ── Firmalar ───────────────────────────────────────────────────────────────

    /// <summary>Firma listesini döner.</summary>
    [HttpGet("firms")]
    public async Task<IActionResult> GetFirms([FromQuery] bool activeOnly = false, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetFirmsQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Firma detayını döner (platformlar + entegrasyonlar dahil).</summary>
    [HttpGet("firms/{id:guid}")]
    public async Task<IActionResult> GetFirmDetail(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetFirmDetailQuery(id), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Yeni firma oluşturur.</summary>
    [HttpPost("firms")]
    public async Task<IActionResult> CreateFirm([FromBody] CreateFirmRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateFirmCommand(request.Code, request.NameI18n, request.TaxOffice, request.TaxNumber,
                request.Address, request.Phone, request.Email, request.IsMain, request.PriceType, request.PriceMultiplier), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Firma bilgilerini günceller.</summary>
    [HttpPut("firms/{id:guid}")]
    public async Task<IActionResult> UpdateFirm(Guid id, [FromBody] UpdateFirmRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateFirmCommand(id, request.NameI18n, request.TaxOffice, request.TaxNumber,
                request.Address, request.Phone, request.Email, request.IsMain, request.PriceType, request.PriceMultiplier, request.IsActive), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ── Firma Platformları ─────────────────────────────────────────────────────

    /// <summary>Firmaya ait platformları listeler.</summary>
    [HttpGet("firms/{firmId:guid}/platforms")]
    public async Task<IActionResult> GetFirmPlatforms(Guid firmId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetFirmPlatformsQuery(firmId), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Firmaya yeni platform ekler.</summary>
    [HttpPost("firms/{firmId:guid}/platforms")]
    public async Task<IActionResult> CreateFirmPlatform(Guid firmId, [FromBody] CreateFirmPlatformRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateFirmPlatformCommand(firmId, request.PlatformTypeId, request.Code, request.NameI18n,
                request.PriceType, request.PriceMultiplier), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    /// <summary>Firma platformunu günceller.</summary>
    [HttpPut("firm-platforms/{id:guid}")]
    public async Task<IActionResult> UpdateFirmPlatform(Guid id, [FromBody] UpdateFirmPlatformRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdateFirmPlatformCommand(id, request.NameI18n, request.PriceType, request.PriceMultiplier, request.IsActive), ct);
        if (result.IsFailure)
            return NotFound(new { success = false, error = result.Error });
        return Ok(new { success = true });
    }

    // ── Firma Entegrasyonları ──────────────────────────────────────────────────

    /// <summary>Firmaya ait entegrasyonları listeler.</summary>
    [HttpGet("firms/{firmId:guid}/integrations")]
    public async Task<IActionResult> GetFirmIntegrations(Guid firmId, [FromQuery] string? serviceType = null, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetFirmIntegrationsQuery(firmId, serviceType), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Firmaya yeni entegrasyon ekler.</summary>
    [HttpPost("firms/{firmId:guid}/integrations")]
    public async Task<IActionResult> CreateFirmIntegration(Guid firmId, [FromBody] CreateFirmIntegrationRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateFirmIntegrationCommand(firmId, request.IntegrationServiceId, request.Name,
                request.Credentials ?? new(), request.Settings ?? new()), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }

    // ── Kargo Kuralları ────────────────────────────────────────────────────────

    /// <summary>Firmaya ait kargo kurallarını listeler.</summary>
    [HttpGet("firms/{firmId:guid}/cargo-rules")]
    public async Task<IActionResult> GetCargoRules(Guid firmId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCargoRulesQuery(firmId), ct);
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Firmaya yeni kargo kuralı ekler.</summary>
    [HttpPost("firms/{firmId:guid}/cargo-rules")]
    public async Task<IActionResult> CreateCargoRule(Guid firmId, [FromBody] CreateCargoRuleRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateCargoRuleCommand(firmId, request.FirmIntegrationId, request.RuleType,
                request.PaymentType, request.NeighborhoodId, request.CityId, request.Priority), ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, error = result.Error });
        return Created(string.Empty, new { success = true, data = new { id = result.Value } });
    }
}

// ── Request Modelleri ──────────────────────────────────────────────────────────

public record CreateFirmRequest(
    string Code,
    Dictionary<string, string> NameI18n,
    string TaxOffice,
    string TaxNumber,
    string Address,
    string Phone,
    string Email,
    bool IsMain,
    string PriceType,
    decimal? PriceMultiplier
);

public record UpdateFirmRequest(
    Dictionary<string, string> NameI18n,
    string TaxOffice,
    string TaxNumber,
    string Address,
    string Phone,
    string Email,
    bool IsMain,
    string PriceType,
    decimal? PriceMultiplier,
    bool IsActive
);

public record CreateFirmPlatformRequest(
    Guid PlatformTypeId,
    string Code,
    Dictionary<string, string> NameI18n,
    string? PriceType,
    decimal? PriceMultiplier
);

public record UpdateFirmPlatformRequest(
    Dictionary<string, string> NameI18n,
    string? PriceType,
    decimal? PriceMultiplier,
    bool IsActive
);

public record CreateFirmIntegrationRequest(
    Guid IntegrationServiceId,
    string? Name,
    Dictionary<string, object>? Credentials,
    Dictionary<string, object>? Settings
);

public record CreateCargoRuleRequest(
    Guid FirmIntegrationId,
    string RuleType,
    string? PaymentType,
    Guid? NeighborhoodId,
    Guid? CityId,
    int Priority
);

public record CreateExpenseTypeRequest(
    string Code,
    Dictionary<string, string> NameI18n,
    bool IsItemLevel,
    decimal DefaultTaxRate,
    int SortOrder
);
