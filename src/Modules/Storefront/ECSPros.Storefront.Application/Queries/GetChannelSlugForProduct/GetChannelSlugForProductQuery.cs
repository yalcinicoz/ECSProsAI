using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelSlugForProduct;

/// <summary>
/// Ürün URL aktarımı Aşama 2: /urun/{code}?color= isteğini kanonik slug'a (301) yönlendirmek için
/// ürünün (o platformdaki) slug'ını çözer. colorValueId verilirse o rengin slug'lı temsilci
/// varyantı tercih edilir; yoksa/eşleşmezse ürünün ilk slug'lı varyantı. Slug yoksa null
/// (çağıran /urun/{code}'u normal render eder — güvenlik ağı).
/// NOT: catDb ve sfDb ayrı DbContext — tek sorguda karıştırılamaz (EF çeviremez); ayrı adımlar.
/// </summary>
public record GetChannelSlugForProductQuery(Guid FirmPlatformId, string ProductCode, Guid? ColorValueId)
    : IRequest<Result<string?>>;

public class GetChannelSlugForProductQueryHandler(IStorefrontDbContext sfDb, ICatalogDbContext catDb)
    : IRequestHandler<GetChannelSlugForProductQuery, Result<string?>>
{
    public async Task<Result<string?>> Handle(GetChannelSlugForProductQuery request, CancellationToken ct)
    {
        // 1) Ürünün varyant id'leri (catDb).
        var variantIds = await catDb.ProductVariants.AsNoTracking()
            .Where(v => v.Product.Code == request.ProductCode)
            .Select(v => v.Id)
            .ToListAsync(ct);
        if (variantIds.Count == 0)
            return Result.Success<string?>(null);

        // 2) Bu varyantların bu platformdaki slug'ları (sfDb).
        var sluglar = await sfDb.ChannelVariants.AsNoTracking()
            .Where(cv => cv.FirmPlatformId == request.FirmPlatformId && cv.Slug != null
                      && variantIds.Contains(cv.VariantId))
            .Select(cv => new { cv.VariantId, cv.Slug })
            .ToListAsync(ct);
        if (sluglar.Count == 0)
            return Result.Success<string?>(null);

        // 3) Renk verildiyse o rengi taşıyan slug'lı varyantı tercih et (catDb).
        if (request.ColorValueId is { } renk)
        {
            var sluggedIds = sluglar.Select(x => x.VariantId).ToList();
            var renkVaryantIds = await catDb.ProductVariantAttributes.AsNoTracking()
                .Where(va => va.AttributeValueId == renk && sluggedIds.Contains(va.VariantId))
                .Select(va => va.VariantId)
                .ToListAsync(ct);
            var renkEslesen = sluglar.FirstOrDefault(x => renkVaryantIds.Contains(x.VariantId));
            if (renkEslesen is not null)
                return Result.Success<string?>(renkEslesen.Slug);
        }

        return Result.Success<string?>(sluglar[0].Slug);
    }
}
