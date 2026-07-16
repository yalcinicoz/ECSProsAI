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
            // G10: kompozisyon ziyaretçi segmentine göre (taban her sayfada çözer)
            var segment = ViewData["MsSegment"] as VisitorSegment ?? VisitorSegment.Misafir;
            var (_, bloklar) = await composer.ComposeAsync(platform.Id, "homepage", segment, ct);
            if (bloklar.Count > 0)
                ViewData["MsVitrinBloklar"] = await vitrinBuilder.KurAsync(platform.Id, bloklar, ct);
        }

        return View();
    }

    // Faz 5 (2026-07-16): üyeliksiz kargo takip — tasarımın demo verili sayfaları.
    // ⏰ Kargo servisi entegrasyonu gündeme gelince gerçek takip servisine bağlanacak
    // (sözleşme: /opt/misharix/md/backend-devir-entegrasyonlari.md).
    [HttpGet("/uyeliksiz-kargo-takip")]
    public IActionResult UyelikSizKargoTakip() => View("uyeliksizKargoTakip");

    [HttpGet("/uyeliksiz-kargo-takip-sonuc")]
    public IActionResult UyelikSizKargoTakipSonuc() => View("uyeliksizKargoTakipSonuc");
}
