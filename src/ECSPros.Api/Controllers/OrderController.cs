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
}
