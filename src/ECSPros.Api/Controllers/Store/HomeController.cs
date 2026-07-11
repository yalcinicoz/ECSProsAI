using ECSPros.Api.Models.Store;
using ECSPros.Api.Services;
using ECSPros.Api.Services.Store;
using ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Storefront ana sayfası (Razor). Sayfa controller'ları iş mantığını
/// api/store/* ile aynı MediatR handler'ları üzerinden çağırır (plan 3.4).
/// G5: yayınlanmış vitrin varsa ana sayfa PageComposer'dan render edilir
/// (store API ile aynı kompozisyon); yayın yoksa B6 geçici kompozisyonu
/// (kapsül şeridi + kategori carousel'leri) yedek olarak sürer — G8'de admin
/// vitrini yönetir hale gelince bu yedek kaldırılır. Vitrin yolunda IMemoryCache
/// KULLANILMAZ (yayın anında tazelik); versiyonlu cache G7'de gelir.
/// </summary>
public class HomeController(
    IServiceScopeFactory scopeFactory,
    IStoreContext storeContext,
    IMemoryCache cache,
    IPageComposer composer,
    IVitrinVmBuilder vitrinBuilder) : StorePageController
{
    private const int VitrinUrunSayisi = 10;
    private const int EnFazlaVitrin = 3;
    private static readonly TimeSpan CacheSuresi = TimeSpan.FromMinutes(15);

    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        var nav = ViewData["MsNavigasyon"] as NavigasyonVm ?? NavigasyonVm.Bos;

        if (platform is not null)
        {
            var (_, bloklar) = await composer.ComposeAsync(platform.Id, "homepage", ct);
            if (bloklar.Count > 0)
            {
                ViewData["MsVitrinBloklar"] = await vitrinBuilder.KurAsync(platform.Id, bloklar, ct);
                return View();
            }
        }

        var vm = platform is null
            ? AnaSayfaVm.Bos
            : await cache.GetOrCreateAsync($"store-home:{platform.Id}", async entry =>
              {
                  entry.AbsoluteExpirationRelativeToNow = CacheSuresi;
                  return await VmKurAsync(nav, ct);
              }) ?? AnaSayfaVm.Bos;

        ViewData["MsAnaSayfa"] = vm;
        return View();
    }

    private async Task<AnaSayfaVm> VmKurAsync(NavigasyonVm nav, CancellationToken ct)
    {
        var kapsuller = new List<KapsulKategoriVm>();
        var vitrinler = new List<VitrinVm>();

        // Kök kategorilerin ürünleri paralel çekilir — her görev kendi DI scope'unda
        // (scoped DbContext paylaşımı güvenli değil). Soğuk yükleme, en yavaş tek
        // sorgu süresine iner; sonrası 15 dk cache'ten gelir.
        var gorevler = nav.Kokler.Select(async kok =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var sonuc = await mediator.Send(
                new GetChannelCategoryProductsQuery(kok.Id, 1, VitrinUrunSayisi), ct);
            return (Kok: kok, Sonuc: sonuc);
        }).ToList();

        foreach (var (kok, sonuc) in await Task.WhenAll(gorevler))
        {
            if (sonuc.IsFailure || !sonuc.Value!.Items.Any())
                continue;

            var kartlar = sonuc.Value.Items.Select(UrunKartMap.KartaCevir).ToList();

            // Kapsül görseli: kategorinin kendi görseli yoksa ilk ürün görseli
            // (mishar kanal kategorileri görselsiz — B1 bulgusu). Görselsiz kapsül basılmaz.
            var gorsel = kok.GorselUrl ?? kartlar.FirstOrDefault(k => k.GorselUrl is not null)?.GorselUrl;
            if (gorsel is not null)
                kapsuller.Add(new KapsulKategoriVm(kok.Ad, kok.Slug, gorsel));

            if (vitrinler.Count < EnFazlaVitrin)
                vitrinler.Add(new VitrinVm(kok.Ad, kok.Slug, kartlar));
        }

        return new AnaSayfaVm(kapsuller, vitrinler);
    }
}
