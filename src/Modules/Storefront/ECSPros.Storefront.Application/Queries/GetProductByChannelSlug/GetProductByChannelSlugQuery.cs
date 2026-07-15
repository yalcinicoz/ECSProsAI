using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetProductByChannelSlug;

/// <summary>
/// Gerçek (legacy) URL slug'ından ürünü çözer (ürün URL aktarımı, 2026-07-15): bu platformda
/// (host'tan) verilen slug'a sahip channel_variant → ürün Code + o varyantın renk değer id'si
/// (detay ?color= için). Bulunamazsa null döner (çağıran 404). Kök /{slug} kategori değilse denenir.
/// </summary>
public record GetProductByChannelSlugQuery(Guid FirmPlatformId, string Slug)
    : IRequest<Result<ProductByChannelSlugDto?>>;

public record ProductByChannelSlugDto(string ProductCode, Guid? ColorValueId);

public class GetProductByChannelSlugQueryHandler(IStorefrontDbContext sfDb, ICatalogDbContext catDb)
    : IRequestHandler<GetProductByChannelSlugQuery, Result<ProductByChannelSlugDto?>>
{
    public async Task<Result<ProductByChannelSlugDto?>> Handle(
        GetProductByChannelSlugQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Slug))
            return Result.Success<ProductByChannelSlugDto?>(null);

        var variantId = await sfDb.ChannelVariants.AsNoTracking()
            .Where(cv => cv.FirmPlatformId == request.FirmPlatformId && cv.Slug == request.Slug)
            .Select(cv => (Guid?)cv.VariantId)
            .FirstOrDefaultAsync(ct);
        if (variantId is null)
            return Result.Success<ProductByChannelSlugDto?>(null);

        var urun = await catDb.ProductVariants.AsNoTracking()
            .Where(v => v.Id == variantId.Value)
            .Select(v => new { v.ProductId, v.Product.Code })
            .FirstOrDefaultAsync(ct);
        if (urun is null)
            return Result.Success<ProductByChannelSlugDto?>(null);

        // Varyantın renk ekseni değeri (detayda ?color= ile o renk seçilsin): filtre_rengi
        // öncelikli, yoksa serbest-metin "renk". Bulunamazsa null (detay ilk rengi seçer).
        var renkDeger = await catDb.ProductVariantAttributes.AsNoTracking()
            .Where(va => va.VariantId == variantId.Value
                      && (va.AttributeType.Code == "filtre_rengi" || va.AttributeType.Code == "renk"))
            .OrderByDescending(va => va.AttributeType.Code == "filtre_rengi")
            .Select(va => (Guid?)va.AttributeValueId)
            .FirstOrDefaultAsync(ct);

        return Result.Success<ProductByChannelSlugDto?>(new ProductByChannelSlugDto(urun.Code, renkDeger));
    }
}
