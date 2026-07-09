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
        ViewData["MsUye"] = await services.GetRequiredService<IStoreMemberSession>()
            .MevcutUyeAsync(context.HttpContext);
        ViewData["MsNavigasyon"] = platform is null
            ? NavigasyonVm.Bos
            : await NavigasyonuGetirAsync(services, platform.Id, context.HttpContext.RequestAborted);

        // C8/D3: CMS legal sayfaları — sepet/ödeme sözleşme modalları + nav'daki kayıt
        // belge modalı (üyelik/KVKK) bunlardan beslenir; nav her sayfada olduğundan
        // yükleme burada (SepetController'dan taşındı), platform başına 5 dk cache.
        if (platform is not null)
            ViewData["MsSozlesmeler"] = await SozlesmeleriGetirAsync(
                services, platform.Id, context.HttpContext.RequestAborted);

        await next();
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
