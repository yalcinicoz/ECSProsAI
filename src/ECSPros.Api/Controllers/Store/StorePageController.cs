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
        // gösterir; G10 kural motoru da aynı segmenti kullanacak (il haritası süreç içi
        // cache'li; üye sorgusu yalnız oturumlularda).
        ViewData["MsSegment"] = await services
            .GetRequiredService<ECSPros.Api.Services.Store.IVisitorSegmentResolver>()
            .ResolveAsync(context.HttpContext, uye?.MemberId);
        ViewData["MsNavigasyon"] = platform is null
            ? NavigasyonVm.Bos
            : await NavigasyonuGetirAsync(services, platform.Id, context.HttpContext.RequestAborted);

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
            // PageComposer G7 versiyonlu cache'iyle). Blok yoksa _AnaNavigasyonDuyuru
            // tasarımın statik metinlerini basar (F4 footer yedek deseni).
            ViewData["MsDuyurular"] = await DuyurulariGetirAsync(
                services, platform.Id, context.HttpContext.RequestAborted);
        }

        await next();
    }

    private static async Task<List<string>?> DuyurulariGetirAsync(
        IServiceProvider services, Guid platformId, CancellationToken ct)
    {
        var composer = services.GetRequiredService<ECSPros.Api.Services.Store.IPageComposer>();
        var (_, bloklar) = await composer.ComposeAsync(platformId, "global-top", ct);
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
        IServiceProvider services, Guid platformId, CancellationToken ct)
    {
        var cache = services.GetRequiredService<IMemoryCache>();

        var vm = await cache.GetOrCreateAsync($"store-nav:{platformId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = NavCacheSuresi;

            var mediator = services.GetRequiredService<IMediator>();
            var sonuc = await mediator.Send(new GetChannelCategoriesQuery(platformId, ActiveOnly: true), ct);
            if (sonuc.IsFailure)
                return NavigasyonVm.Bos;

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
                        Dallar(k.Id)))
                    .ToList();

            return new NavigasyonVm(Dallar(null));
        });

        return vm ?? NavigasyonVm.Bos;
    }
}
