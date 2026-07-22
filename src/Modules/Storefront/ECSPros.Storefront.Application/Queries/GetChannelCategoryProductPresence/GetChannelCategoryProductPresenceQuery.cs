using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelCategoryProductPresence;

/// <summary>
/// Platformun yayındaki kanal kategorilerinden, MÜŞTERİNİN GERÇEKTEN ürün göreceği
/// olanların Id seti (navigasyonda boş kategorileri gizlemek için). Kategori başına
/// ürün kimlikleri, liste sayfasının kullandığı geçit zincirinin TEK KAYNAĞI olan
/// <see cref="GetChannelCategoryProductsQueryHandler.ResolveCategoryProductIds"/> ile
/// çözülür — kural motoru + satış anahtarı + kanal seçimi/durdurma + stok görünürlüğü
/// + görsel şartı birebir aynı uygulanır; menü ile sayfa asla çelişmez.
/// Not: hesap ucuz değildir — çağıran taraf sonucu cache'lemeli (nav 15 dk tutuyor).
/// </summary>
public record GetChannelCategoryProductPresenceQuery(
    Guid FirmPlatformId,
    bool ShowOutOfStock = false,
    DateTime? OutOfStockSince = null) : IRequest<Result<HashSet<Guid>>>;

public class GetChannelCategoryProductPresenceQueryHandler(
    IStorefrontDbContext sfDb,
    ICatalogDbContext catDb,
    IStockService stockService,
    IChannelPricingService pricingService,
    IInStockProductProvider inStock)
    : IRequestHandler<GetChannelCategoryProductPresenceQuery, Result<HashSet<Guid>>>
{
    public async Task<Result<HashSet<Guid>>> Handle(
        GetChannelCategoryProductPresenceQuery request, CancellationToken ct)
    {
        var kategoriler = await sfDb.ChannelCategories
            .AsNoTracking()
            .Where(c => c.FirmPlatformId == request.FirmPlatformId && c.Status == "published")
            .ToListAsync(ct);

        var dolu = new HashSet<Guid>();
        foreach (var cat in kategoriler)
        {
            var ids = await GetChannelCategoryProductsQueryHandler.ResolveCategoryProductIds(
                sfDb, catDb, stockService, pricingService, inStock,
                cat, cat.Id, request.ShowOutOfStock, request.OutOfStockSince, ct);
            if (ids.Count > 0)
                dolu.Add(cat.Id);
        }

        return Result.Success(dolu);
    }
}
