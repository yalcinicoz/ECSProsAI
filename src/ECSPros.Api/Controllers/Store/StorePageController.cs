using ECSPros.Api.Models.Store;
using ECSPros.Api.Services;
using ECSPros.Storefront.Application.Queries.GetChannelCategories;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Tüm storefront Razor sayfa controller'larının tabanı. Her istekte aktif platformu
/// ve navigasyon kategori ağacını ViewData'ya koyar (layout'taki nav partial'ları
/// ViewData üzerinden okur — misharix'in ViewData kalıbıyla uyumlu, partial include
/// satırları değişmeden çalışır). Ağaç platform başına 5 dk IMemoryCache'te tutulur.
/// </summary>
public abstract class StorePageController : Controller
{
    private static readonly TimeSpan NavCacheSuresi = TimeSpan.FromMinutes(5);

    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var storeContext = services.GetRequiredService<IStoreContext>();
        var platform = await storeContext.GetPlatformAsync(context.HttpContext.RequestAborted);

        ViewData["MsPlatform"] = platform;
        // D1: HttpOnly cookie'deki JWT'den SSR kimliği — sayfalar ViewData["MsUye"] ile
        // üye bilinen render yapabilir (null = misafir; JS localStorage akışı bağımsız).
        var uye = await services.GetRequiredService<IStoreMemberSession>()
            .MevcutUyeAsync(context.HttpContext);
        ViewData["MsUye"] = uye;
        // G9b: ziyaretçi segmenti — duyuru barındaki şehir çipi mevcut konumu buradan
        // gösterir; G10 kural motoru (PageComposer) aynı segmentle kompozisyon yapar
        // (il haritası süreç içi cache'li; üye sorgusu yalnız oturumlularda).
        var segment = await services
            .GetRequiredService<ECSPros.Api.Services.Store.IVisitorSegmentResolver>()
            .ResolveAsync(context.HttpContext, uye?.MemberId);
        ViewData["MsSegment"] = segment;
        ViewData["MsNavigasyon"] = platform is null
            ? NavigasyonVm.Bos
            : await NavigasyonuGetirAsync(services, platform, context.HttpContext.RequestAborted);

        // C8/D3: CMS legal sayfaları — sepet/ödeme sözleşme modalları + nav'daki kayıt
        // belge modalı (üyelik/KVKK) bunlardan beslenir; nav her sayfada olduğundan
        // yükleme burada (SepetController'dan taşındı), platform başına 5 dk cache.
        if (platform is not null)
        {
            ViewData["MsSozlesmeler"] = await SozlesmeleriGetirAsync(
                services, platform.Id, context.HttpContext.RequestAborted);
            // F4: footer kolonları admin'in "footer" kodlu nav menüsünden (varsa);
            // yoksa _Footer tasarımın statik kolonlarını basar.
            ViewData["MsFooterMenu"] = await FooterMenusunuGetirAsync(
                services, platform.Id, context.HttpContext.RequestAborted);
            // G8: duyuru şeridi metinleri vitrin "announcement" bloklarından (global-top;
            // PageComposer G7 versiyonlu cache'iyle; G10: öğe kuralları segmentle süzülür).
            // Blok yoksa _AnaNavigasyonDuyuru tasarımın statik metinlerini basar (F4 deseni).
            ViewData["MsDuyurular"] = await DuyurulariGetirAsync(
                services, platform.Id, segment, context.HttpContext.RequestAborted);
        }

        await next();
    }

    private static async Task<List<string>?> DuyurulariGetirAsync(
        IServiceProvider services, Guid platformId,
        ECSPros.Api.Services.Store.VisitorSegment segment, CancellationToken ct)
    {
        var composer = services.GetRequiredService<ECSPros.Api.Services.Store.IPageComposer>();
        // global-top duyuru barı üye bağlamlı ürün kaynağı taşımaz — memberId gerekmez
        var (_, bloklar) = await composer.ComposeAsync(platformId, "global-top", segment, null, ct);
        var metinler = bloklar
            .Where(b => b.BlockType == "announcement")
            .SelectMany(b => b.Items)
            .Select(i => i.Title.TryGetValue("tr", out var tr) ? tr : i.Title.Values.FirstOrDefault() ?? "")
            .Where(m => m.Length > 0)
            .ToList();
        return metinler.Count > 0 ? metinler : null;
    }

    private static async Task<ECSPros.Storefront.Application.Queries.GetNavigationMenuDetail.NavigationMenuDetailDto?> FooterMenusunuGetirAsync(
        IServiceProvider services, Guid platformId, CancellationToken ct)
    {
        var cache = services.GetRequiredService<IMemoryCache>();
        return await cache.GetOrCreateAsync<ECSPros.Storefront.Application.Queries.GetNavigationMenuDetail.NavigationMenuDetailDto?>(
            $"store-footer-menu:{platformId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var sonuc = await services.GetRequiredService<IMediator>().Send(
                new ECSPros.Storefront.Application.Queries.GetStoreNavigationMenu.GetStoreNavigationMenuQuery(
                    "footer", platformId), ct);
            return sonuc.IsSuccess ? sonuc.Value : null;
        });
    }

    private static async Task<List<ECSPros.Cms.Application.Queries.GetStoreLegalPages.StoreLegalPageDto>> SozlesmeleriGetirAsync(
        IServiceProvider services, Guid platformId, CancellationToken ct)
    {
        var cache = services.GetRequiredService<IMemoryCache>();
        return await cache.GetOrCreateAsync($"store-legal:{platformId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var sonuc = await services.GetRequiredService<IMediator>().Send(
                new ECSPros.Cms.Application.Queries.GetStoreLegalPages.GetStoreLegalPagesQuery(platformId), ct);
            return sonuc.IsSuccess
                ? sonuc.Value!
                : new List<ECSPros.Cms.Application.Queries.GetStoreLegalPages.StoreLegalPageDto>();
        }) ?? new List<ECSPros.Cms.Application.Queries.GetStoreLegalPages.StoreLegalPageDto>();
    }

    private static async Task<NavigasyonVm> NavigasyonuGetirAsync(
        IServiceProvider services, StorePlatformBilgisi platform, CancellationToken ct)
    {
        var platformId = platform.Id;
        var cache = services.GetRequiredService<IMemoryCache>();

        var vm = await cache.GetOrCreateAsync($"store-nav:{platformId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = NavCacheSuresi;

            var mediator = services.GetRequiredService<IMediator>();
            var sonuc = await mediator.Send(new GetChannelCategoriesQuery(platformId, ActiveOnly: true), ct);
            if (sonuc.IsFailure)
                return NavigasyonVm.Bos;

            // Kategori başına "müşteri gerçekten ürün görecek mi" seti. Hesap liste
            // sayfasıyla aynı geçit zincirini koştuğundan ucuz değildir: ayrı anahtarla
            // 15 dk cache'lenir ve YOKSA ARKA PLANDA hesaplanır — istek hiç bekletilmez,
            // set hazır olana kadar menü budanmadan gösterilir (güvenli varsayılan).
            var doluIdler = PresenceGetirVeyaBaslat(services, platform);

            var kategoriler = sonuc.Value!;
            List<NavKategori> Dallar(Guid? parentId) =>
                kategoriler
                    .Where(k => k.ParentId == parentId)
                    .OrderBy(k => k.SortOrder)
                    .Select(k => new NavKategori(
                        k.Id,
                        k.NameI18n.TryGetValue("tr", out var ad) ? ad : k.NameI18n.Values.FirstOrDefault() ?? k.Slug,
                        k.Slug,
                        k.DisplayImageUrl,
                        k.BadgeLabel,
                        Dallar(k.Id),
                        UrunVar: doluIdler is null || doluIdler.Contains(k.Id)))
                    .ToList();

            // Tamamı boş dallar (kendisinde ve altlarında hiç ürün yok) menülerde
            // gösterilmez; tam ağaç doğrudan URL erişimi için TumKokler'de saklanır.
            static List<NavKategori> BosDallariBuda(IReadOnlyList<NavKategori> dallar) =>
                dallar
                    .Where(d => d.DalindaUrunVar)
                    .Select(d => d with { Cocuklar = BosDallariBuda(d.Cocuklar) })
                    .ToList();

            var tumu = Dallar(null);
            return new NavigasyonVm(BosDallariBuda(tumu), tumu);
        });

        return vm ?? NavigasyonVm.Bos;
    }

    private static readonly TimeSpan PresenceCacheSuresi = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Boş-olmayan kategori Id setini cache'ten verir; yoksa null döner ve hesabı
    /// arka planda başlatır (tekilleştirilmiş). Hesap bitince nav cache'i düşürülür ki
    /// bir sonraki istek menüyü budanmış kursun.
    /// </summary>
    private static HashSet<Guid>? PresenceGetirVeyaBaslat(IServiceProvider services, StorePlatformBilgisi platform)
    {
        var cache = services.GetRequiredService<IMemoryCache>();
        var anahtar = $"store-nav-presence:{platform.Id}";
        if (cache.TryGetValue(anahtar, out HashSet<Guid>? hazir))
            return hazir;

        var calisiyorAnahtar = anahtar + ":calisiyor";
        if (cache.TryGetValue(calisiyorAnahtar, out bool _))
            return null; // hesap zaten yolda
        cache.Set(calisiyorAnahtar, true, TimeSpan.FromMinutes(2));

        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var platformId = platform.Id;
        var stokGoster = platform.StokBitenGoster;
        var stokTarih = platform.StokBitenGosterTarih;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var med = scope.ServiceProvider.GetRequiredService<IMediator>();
                var r = await med.Send(
                    new ECSPros.Storefront.Application.Queries.GetChannelCategoryProductPresence
                        .GetChannelCategoryProductPresenceQuery(platformId, stokGoster, stokTarih),
                    CancellationToken.None);
                if (r.IsSuccess)
                {
                    cache.Set(anahtar, r.Value!, PresenceCacheSuresi);
                    cache.Remove($"store-nav:{platformId}"); // menü bir sonraki istekte budansın
                }
            }
            catch
            {
                // sessiz: presence hesaplanamazsa menü budanmadan gösterilmeye devam eder
            }
            finally
            {
                cache.Remove(calisiyorAnahtar);
            }
        });
        return null;
    }
}
