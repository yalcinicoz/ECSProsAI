using ECSPros.Api.Services.Store;
using ECSPros.Storefront.Application.Queries.GetNewsletterSubscriptions;
using ECSPros.Storefront.Application.Queries.GetSavedSearchesForAdmin;
using ECSPros.Storefront.Application.Queries.GetStockAlertsForAdmin;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// H8: bildirim operasyonları (admin). Favori arama taraması normalde
/// SavedSearchNotifyWorker'la periyodik koşar — buradaki tetik, "şimdi tara" operasyon
/// ihtiyacı + E2E determinizmi içindir (günde-1 sınırı LastNotifiedAt'ta olduğundan
/// elle tetiklemek yinelenen e-posta üretmez).
/// P5: izleme listeleri — stok alarmları, kayıtlı aramalar, bülten aboneleri.
/// </summary>
[ApiController]
[Route("api/store-notifications")]
[Authorize]
public class StoreNotificationsController(
    ISavedSearchNotifier savedSearchNotifier,
    IMediator mediator) : ControllerBase
{
    [HttpPost("saved-search-scan")]
    public async Task<IActionResult> RunSavedSearchScan(CancellationToken ct)
    {
        var gonderilen = await savedSearchNotifier.RunOnceAsync(ct);
        return Ok(new { success = true, data = new { sent = gonderilen } });
    }

    [HttpGet("stock-alerts")]
    public async Task<IActionResult> GetStockAlerts(
        [FromQuery] string? status = null,
        [FromQuery] Guid? firmPlatformId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetStockAlertsForAdminQuery(status, firmPlatformId, search, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("saved-searches")]
    public async Task<IActionResult> GetSavedSearches(
        [FromQuery] bool? notifyEnabled = null,
        [FromQuery] Guid? firmPlatformId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetSavedSearchesForAdminQuery(notifyEnabled, firmPlatformId, search, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }

    [HttpGet("newsletter-subscriptions")]
    public async Task<IActionResult> GetNewsletterSubscriptions(
        [FromQuery] bool? isActive = null,
        [FromQuery] Guid? firmPlatformId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetNewsletterSubscriptionsQuery(isActive, firmPlatformId, search, page, pageSize), ct);
        if (result.IsFailure) return BadRequest(new { success = false, error = result.Error });
        return Ok(new { success = true, data = result.Value });
    }
}
