using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetProductChannelCategoryChain;

/// <summary>
/// Bir ürünün ait olduğu kanal kategori zincirini (kök → yaprak) bulur — ürün detay
/// breadcrumb'ı için. Kategoriler filtre tanımlı (FillType=filter/mixed) olduğundan
/// ters eşleme kural değerlendirmesiyle yapılır: ürünün grubu + ürün seviyesi özellik
/// değerleri, kategorinin ProductGroupIds/AttributeFilters kurallarıyla karşılaştırılır
/// (listeleme tarafındaki ProductFilterHelper ile aynı semantik). Manuel atamalar
/// (ChannelCategoryProduct) dahildir; IsExcluded satırı kategoriyi eler. Birden çok
/// kategori eşleşirse en derin olan (eşitlikte SortOrder küçük olan) seçilir.
/// Fiyat/stok gibi diğer kural alanları breadcrumb için değerlendirilmez.
/// </summary>
public record GetProductChannelCategoryChainQuery(
    Guid FirmPlatformId,
    Guid ProductId,
    // 2026-08-26: ziyaretçinin GELDİĞİ kategori sayfası (Referer) — aday ya da adayın atasıysa
    // zincir o yoldan kurulur ("Tesettür Etek listesinden geldim → breadcrumb Tesettür Etek").
    string? PreferredSlug = null) : IRequest<Result<List<ProductCategoryChainItemDto>>>;

public record ProductCategoryChainItemDto(Guid Id, string Slug, Dictionary<string, string> NameI18n);

public class GetProductChannelCategoryChainQueryHandler(
    IStorefrontDbContext sfDb,
    ICatalogDbContext catDb)
    : IRequestHandler<GetProductChannelCategoryChainQuery, Result<List<ProductCategoryChainItemDto>>>
{
    private sealed record Kategori(
        Guid Id, Guid? ParentId, string Slug, Dictionary<string, string> NameI18n,
        string FillType, Dictionary<string, object>? FilterDef, int SortOrder);

    public async Task<Result<List<ProductCategoryChainItemDto>>> Handle(
        GetProductChannelCategoryChainQuery request, CancellationToken ct)
    {
        var kategoriler = await sfDb.ChannelCategories.AsNoTracking()
            .Where(c => c.FirmPlatformId == request.FirmPlatformId && c.Status == "published")
            .Select(c => new Kategori(
                c.Id, c.ParentId, c.Slug, c.NameI18n, c.FillType, c.FilterDef, c.SortOrder))
            .ToListAsync(ct);

        if (kategoriler.Count == 0)
            return Result.Success(new List<ProductCategoryChainItemDto>());

        var grupId = await catDb.Products.AsNoTracking()
            .Where(p => p.Id == request.ProductId)
            .Select(p => (Guid?)p.ProductGroupId)
            .FirstOrDefaultAsync(ct);

        if (grupId is null)
            return Result.Success(new List<ProductCategoryChainItemDto>());

        var degerlerTipe = (await catDb.ProductAttributes.AsNoTracking()
                .Where(a => a.ProductId == request.ProductId && a.AttributeValueId != null)
                .Select(a => new { a.AttributeTypeId, ValueId = a.AttributeValueId!.Value })
                .ToListAsync(ct))
            .GroupBy(a => a.AttributeTypeId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ValueId).ToHashSet());

        var manuelSatirlar = await sfDb.ChannelCategoryProducts.AsNoTracking()
            .Where(cp => cp.ProductId == request.ProductId)
            .Select(cp => new { cp.ChannelCategoryId, cp.IsExcluded })
            .ToListAsync(ct);
        var dahil = manuelSatirlar.Where(r => !r.IsExcluded).Select(r => r.ChannelCategoryId).ToHashSet();
        var haric = manuelSatirlar.Where(r => r.IsExcluded).Select(r => r.ChannelCategoryId).ToHashSet();

        bool KurallarEslesiyor(Dictionary<string, object>? filterDef)
        {
            var rules = CategoryFilterRules.From(filterDef);
            if (rules is null)
                return false;

            // Zaman-pencereli koleksiyonlar ("Yeni Gelenler" vb.) breadcrumb adayı olmaz:
            // pencere boşalınca breadcrumb boş sayfaya götürüyordu (kadin-yeni-gelenler vakası).
            if (rules.ZamanPenceresiVar)
                return false;

            var grupKurali = rules.ProductGroupIds is { Count: > 0 };
            var ozellikKurali = rules.AttributeFilters is { Count: > 0 };

            // Değerlendirebildiğimiz hiçbir kural yoksa eşleşme sayma — aksi halde
            // yalnızca fiyat kurallı kategoriler her ürünün breadcrumb adayı olur.
            if (!grupKurali && !ozellikKurali)
                return false;

            if (grupKurali && !rules.ProductGroupIds!.Contains(grupId.Value))
                return false;

            if (ozellikKurali)
            {
                foreach (var af in rules.AttributeFilters!)
                {
                    if (!degerlerTipe.TryGetValue(af.AttributeTypeId, out var sahipOlunan)
                        || !af.ValueIds.Any(sahipOlunan.Contains))
                        return false;
                }
            }

            return true;
        }

        var idIleKategori = kategoriler.ToDictionary(c => c.Id);

        int Derinlik(Kategori k)
        {
            var derinlik = 0;
            while (k.ParentId is { } parentId && idIleKategori.TryGetValue(parentId, out var parent))
            {
                derinlik++;
                k = parent;
            }
            return derinlik;
        }

        var adaylar = kategoriler
            .Where(c => !haric.Contains(c.Id)
                        && (dahil.Contains(c.Id)
                            || (c.FillType is "filter" or "mixed" && KurallarEslesiyor(c.FilterDef))))
            .ToList();

        if (adaylar.Count == 0)
            return Result.Success(new List<ProductCategoryChainItemDto>());

        // Kaynak kategori tercihi: geldiği sayfa aday ya da bir adayın atasıysa seçim o dala daralır.
        if (request.PreferredSlug is { Length: > 0 } tercihSlug
            && kategoriler.FirstOrDefault(c =>
                string.Equals(c.Slug, tercihSlug, StringComparison.OrdinalIgnoreCase)) is { } tercihKat)
        {
            bool TercihYolunda(Kategori aday)
            {
                var m = aday;
                while (true)
                {
                    if (m.Id == tercihKat.Id) return true;
                    if (m.ParentId is { } pid && idIleKategori.TryGetValue(pid, out var parent)) m = parent;
                    else return false;
                }
            }
            var uygun = adaylar.Where(TercihYolunda).ToList();
            if (uygun.Count > 0) adaylar = uygun;
        }

        var yaprak = adaylar
            .OrderByDescending(Derinlik)
            .ThenBy(c => c.SortOrder)
            .First();

        var zincir = new List<ProductCategoryChainItemDto>();
        var mevcut = yaprak;
        while (true)
        {
            zincir.Insert(0, new ProductCategoryChainItemDto(mevcut.Id, mevcut.Slug, mevcut.NameI18n));
            if (mevcut.ParentId is { } parentId && idIleKategori.TryGetValue(parentId, out var parent))
                mevcut = parent;
            else
                break;
        }

        return Result.Success(zincir);
    }
}
