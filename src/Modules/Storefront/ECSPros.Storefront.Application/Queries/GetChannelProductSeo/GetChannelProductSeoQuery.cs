using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelProductSeo;

/// <summary>
/// Ürün detay sayfası SEO metinleri (2026-09-11, kanal bazlı): başlık = kanal meta (channel_products) ?? ürün meta
/// (products.MetaTitleI18n) ?? null (çağıran ürün adını kullanır); açıklama aynı sırayla ?? null (çağıran kısa açıklama).
/// Tek yer: web detayı ve (ileride) mobil detay bu sorguyu kullanır.
/// </summary>
public record GetChannelProductSeoQuery(Guid FirmPlatformId, Guid ProductId, string Lang = "tr") : IRequest<Result<ChannelProductSeoDto>>;
public record ChannelProductSeoDto(string? Title, string? Description);

public class GetChannelProductSeoQueryHandler(IStorefrontDbContext db, ICatalogDbContext catDb) : IRequestHandler<GetChannelProductSeoQuery, Result<ChannelProductSeoDto>>
{
    public async Task<Result<ChannelProductSeoDto>> Handle(GetChannelProductSeoQuery r, CancellationToken ct)
    {
        var cp = await db.ChannelProducts.AsNoTracking()
            .Where(x => x.FirmPlatformId == r.FirmPlatformId && x.ProductId == r.ProductId)
            .Select(x => new { x.MetaTitleI18n, x.MetaDescriptionI18n })
            .FirstOrDefaultAsync(ct);
        var urun = await catDb.Products.AsNoTracking().Where(p => p.Id == r.ProductId)
            .Select(p => new { p.MetaTitleI18n, p.MetaDescriptionI18n })
            .FirstOrDefaultAsync(ct);
        string? Sec(Dictionary<string, string>? m) => m is not null && m.TryGetValue(r.Lang, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
        return Result.Success(new ChannelProductSeoDto(
            Sec(cp?.MetaTitleI18n) ?? Sec(urun?.MetaTitleI18n),
            Sec(cp?.MetaDescriptionI18n) ?? Sec(urun?.MetaDescriptionI18n)));
    }
}
