using ECSPros.Storefront.Application.Commands.CreateStockAlert;
using ECSPros.Storefront.Application.Queries.GetMemberStockAlerts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// C9: "Stok gelince haber ver" — üye tükenen varyant için bildirim aboneliği açar.
/// Bildirim gönderimi Faz H'de; kayıtlar storefront.stock_alerts'te birikir.
/// </summary>
[ApiController]
[Route("api/store/stock-alerts")]
[Authorize(Policy = "MemberOnly")]
public class StoreStockAlertController(IMediator mediator) : ControllerBase
{
    private Guid MemberId => Guid.Parse(
        User.FindFirst("sub")?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] StoreStockAlertRequest req, CancellationToken ct)
    {
        var email = User.FindFirst("email")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

        var result = await mediator.Send(new CreateStockAlertCommand(
            req.FirmPlatformId, req.VariantId, MemberId, email,
            req.ProductCode, req.VariantInfo), ct);

        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    /// <summary>Üyenin aktif kayıtlarının varyant id'leri — buton durumunu işaretlemek için.
    /// variantIds virgülle ayrılır; verilmezse üyenin platformdaki tüm aktif kayıtları döner.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(
        [FromQuery] Guid firmPlatformId,
        [FromQuery] string? variantIds,
        CancellationToken ct)
    {
        List<Guid>? idler = null;
        if (!string.IsNullOrWhiteSpace(variantIds))
            idler = variantIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty).ToList();

        var result = await mediator.Send(new GetMemberStockAlertsQuery(firmPlatformId, MemberId, idler), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}

public record StoreStockAlertRequest(
    Guid FirmPlatformId,
    Guid VariantId,
    string? ProductCode = null,
    string? VariantInfo = null);
