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
        ViewData["MsNavigasyon"] = platform is null
            ? NavigasyonVm.Bos
            : await NavigasyonuGetirAsync(services, platform.Id, context.HttpContext.RequestAborted);

        await next();
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
