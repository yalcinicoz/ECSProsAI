using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.FilterSimilarProducts;

/// <summary>
/// Benzer ürünler (2026-08-14): görsel arama sonucundaki aday ürün kodlarını kaynak ürünle
/// AYNI ürün grubu + AYNI cinsiyet (ürün seviyesi "cinsiyet" özelliği, değer kesişimi)
/// kuralına göre süzer. Kaynak ürün listede yer almaz; giriş (benzerlik) sırası korunur.
/// Kaynağın cinsiyet değeri yoksa cinsiyet filtresi uygulanmaz.
/// </summary>
public record FilterSimilarProductCodesQuery(
    string SourceCode,
    List<string> CandidateCodes) : IRequest<Result<List<string>>>;

public class FilterSimilarProductCodesQueryHandler(ICatalogDbContext db)
    : IRequestHandler<FilterSimilarProductCodesQuery, Result<List<string>>>
{
    public async Task<Result<List<string>>> Handle(FilterSimilarProductCodesQuery request, CancellationToken ct)
    {
        if (request.CandidateCodes.Count == 0)
            return Result.Success(new List<string>());

        var kaynak = await db.Products.AsNoTracking()
            .Where(p => p.Code == request.SourceCode)
            .Select(p => new { p.Id, p.ProductGroupId })
            .FirstOrDefaultAsync(ct);
        if (kaynak is null)
            return Result.Success(new List<string>());

        var cinsiyetTipId = await db.AttributeTypes.AsNoTracking()
            .Where(t => t.Code == "cinsiyet")
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);

        var kaynakCinsiyetler = cinsiyetTipId is { } tipId
            ? await db.ProductAttributes.AsNoTracking()
                .Where(pa => pa.ProductId == kaynak.Id && pa.AttributeTypeId == tipId
                          && pa.AttributeValueId != null)
                .Select(pa => pa.AttributeValueId!.Value)
                .ToListAsync(ct)
            : [];

        var adaylar = await db.Products.AsNoTracking()
            .Where(p => request.CandidateCodes.Contains(p.Code)
                     && p.Code != request.SourceCode
                     && p.ProductGroupId == kaynak.ProductGroupId)
            .Select(p => new { p.Id, p.Code })
            .ToListAsync(ct);

        HashSet<string> uygunKodlar;
        if (kaynakCinsiyetler.Count > 0 && adaylar.Count > 0)
        {
            var adayIdler = adaylar.Select(a => a.Id).ToList();
            var uygunIdler = (await db.ProductAttributes.AsNoTracking()
                .Where(pa => adayIdler.Contains(pa.ProductId) && pa.AttributeTypeId == cinsiyetTipId
                          && pa.AttributeValueId != null
                          && kaynakCinsiyetler.Contains(pa.AttributeValueId.Value))
                .Select(pa => pa.ProductId)
                .ToListAsync(ct)).ToHashSet();
            uygunKodlar = adaylar.Where(a => uygunIdler.Contains(a.Id))
                .Select(a => a.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            uygunKodlar = adaylar.Select(a => a.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return Result.Success(request.CandidateCodes.Where(uygunKodlar.Contains).ToList());
    }
}
