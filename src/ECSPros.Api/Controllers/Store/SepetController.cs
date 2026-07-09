using ECSPros.Api.Services;
using ECSPros.Cms.Application.Queries.GetStoreLegalPages;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Sepet sayfası (C1). Sepet istemci-durumludur (localStorage ecspros_sid/ecspros_cart) —
/// satırlar sayfa içi script'le GET /api/store/cart'tan render edilir (B5 mini sepet deseni);
/// SSR yalnız kabuğu verir. Teslimat/ödeme sayfaları C4-C5'te gelecek.
/// </summary>
public class SepetController(IConfiguration configuration) : StorePageController
{
    private static readonly TimeSpan SozlesmeCacheSuresi = TimeSpan.FromMinutes(5);

    // C7: TCKN eşiği — sayfa script'leri banner/guard için okur (asıl güvence checkout'ta)
    // C8: sözleşme içerikleri CMS legal sayfalarından SSR'a taşınır (modal + ödeme bilgi grupları)
    public override async Task OnActionExecutionAsync(
        Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context,
        Microsoft.AspNetCore.Mvc.Filters.ActionExecutionDelegate next)
    {
        ViewData["MsTcknEsik"] = configuration.GetValue<decimal>("Store:TcknThreshold", 13000m);

        var services = context.HttpContext.RequestServices;
        var platform = await services.GetRequiredService<IStoreContext>()
            .GetPlatformAsync(context.HttpContext.RequestAborted);
        if (platform is not null)
        {
            var cache = services.GetRequiredService<IMemoryCache>();
            ViewData["MsSozlesmeler"] = await cache.GetOrCreateAsync(
                $"store-legal:{platform.Id}", async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = SozlesmeCacheSuresi;
                    var sonuc = await services.GetRequiredService<IMediator>().Send(
                        new GetStoreLegalPagesQuery(platform.Id),
                        context.HttpContext.RequestAborted);
                    return sonuc.IsSuccess ? sonuc.Value! : new List<StoreLegalPageDto>();
                }) ?? new List<StoreLegalPageDto>();
        }

        await base.OnActionExecutionAsync(context, next);
    }

    [HttpGet("/sepet")]
    public IActionResult Index() => View("~/Views/Sepet/Index.cshtml");

    /// <summary>C4-b: teslimat adımı — adres seçimi üye gerektirir (sayfa script'i yönetir).</summary>
    [HttpGet("/teslimat")]
    public IActionResult Teslimat() => View("~/Views/Sepet/Teslimat.cshtml");

    /// <summary>C5: ödeme adımı (test modu — K2; tahsilat mock, sipariş C10 checkout'uyla oluşur).</summary>
    [HttpGet("/odeme")]
    public IActionResult Odeme() => View("~/Views/Sepet/Odeme.cshtml");

    /// <summary>C10: sipariş tamamlandı — içerik sessionStorage msSiparisSonucu'ndan.</summary>
    [HttpGet("/siparis-tamamlandi")]
    public IActionResult SiparisTamamlandi() => View("~/Views/Sepet/SiparisTamamlandi.cshtml");
}
