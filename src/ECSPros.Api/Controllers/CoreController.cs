using ECSPros.Core.Application.Queries.GetLanguages;
using ECSPros.Core.Application.Queries.GetOrderStatuses;
using ECSPros.Core.Application.Queries.GetPaymentMethods;
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

    public CoreController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Aktif dilleri listeler.</summary>
    [HttpGet("languages")]
    public async Task<IActionResult> GetLanguages([FromQuery] bool activeOnly = true, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetLanguagesQuery(activeOnly), ct);
        return Ok(new { success = true, data = result.Value });
    }

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
}
