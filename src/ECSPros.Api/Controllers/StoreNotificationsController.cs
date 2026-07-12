using ECSPros.Api.Services.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers;

/// <summary>
/// H8: bildirim operasyonları (admin). Favori arama taraması normalde
/// SavedSearchNotifyWorker'la periyodik koşar — buradaki tetik, "şimdi tara" operasyon
/// ihtiyacı + E2E determinizmi içindir (günde-1 sınırı LastNotifiedAt'ta olduğundan
/// elle tetiklemek yinelenen e-posta üretmez).
/// </summary>
[ApiController]
[Route("api/store-notifications")]
[Authorize]
public class StoreNotificationsController(ISavedSearchNotifier savedSearchNotifier) : ControllerBase
{
    [HttpPost("saved-search-scan")]
    public async Task<IActionResult> RunSavedSearchScan(CancellationToken ct)
    {
        var gonderilen = await savedSearchNotifier.RunOnceAsync(ct);
        return Ok(new { success = true, data = new { sent = gonderilen } });
    }
}
