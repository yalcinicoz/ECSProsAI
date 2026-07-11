using ECSPros.Api.Services;
using ECSPros.Api.Services.Store;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Storefront ana sayfası (Razor). G8: sayfa TAMAMEN vitrin sisteminden render edilir —
/// PageComposer aktif yayınlanmış snapshot'ı çözer (store API ile aynı kompozisyon,
/// G7 versiyonlu cache arkada). B6 geçici kompozisyonu kaldırıldı; varsayılan içerik
/// seed'le v1 olarak yayınlandı (SeedDefaultVitrinAsync), admin Vitrin Yönetimi'nden
/// düzenlenir. Yayın yoksa sayfa bloksuz (boş yerleşim) render olur — bu artık admin
/// kararıdır, kodda yedek kompozisyon yoktur.
/// </summary>
public class HomeController(
    IStoreContext storeContext,
    IPageComposer composer,
    IVitrinVmBuilder vitrinBuilder) : StorePageController
{
    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is not null)
        {
            var (_, bloklar) = await composer.ComposeAsync(platform.Id, "homepage", ct);
            if (bloklar.Count > 0)
                ViewData["MsVitrinBloklar"] = await vitrinBuilder.KurAsync(platform.Id, bloklar, ct);
        }

        return View();
    }
}
