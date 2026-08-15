using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetProductsLeafChannelCategories;

/// <summary>
/// Bir ÜRÜN KÜMESİ için her ürünün ait olduğu en derin (yaprak) kanal kategorisini toplu
/// çözer (2026-08-15) — arama sonucu / benzer ürünler sayfalarında "Kategori" filtresinin
/// sayfadaki ürünlerle sınırlı üretilmesi için. Kural değerlendirmesi
/// GetProductChannelCategoryChain ile AYNI semantik (grup + ürün seviyesi özellik değerleri,
/// manuel dahil/hariç satırları, en derin → SortOrder küçük); farkı: kategoriler ve ürün
/// verileri tek seferde çekilir, N ürün için N sorgu yapılmaz. Kategorisi çözülemeyen ürün
/// sonuçta yer almaz.
/// </summary>
public record GetProductsLeafChannelCategoriesQuery(
    Guid FirmPlatformId,
    List<Guid> ProductIds) : IRequest<Result<List<ProductLeafCategoryDto>>>;

public record ProductLeafCategoryDto(Guid ProductId, Guid CategoryId, string Slug, Dictionary<string, string> NameI18n);

public class GetProductsLeafChannelCategoriesQueryHandler(
    IStorefrontDbContext sfDb,
    ICatalogDbContext catDb)
    : IRequestHandler<GetProductsLeafChannelCategoriesQuery, Result<List<ProductLeafCategoryDto>>>
{
    private sealed record Kategori(
        Guid Id, Guid? ParentId, string Slug, Dictionary<string, string> NameI18n,
        string FillType, Dictionary<string, object>? FilterDef, int SortOrder);

    public async Task<Result<List<ProductLeafCategoryDto>>> Handle(
        GetProductsLeafChannelCategoriesQuery request, CancellationToken ct)
    {
        var sonuc = new List<ProductLeafCategoryDto>();
        var urunIdler = request.ProductIds.Distinct().ToList();
        if (urunIdler.Count == 0)
            return Result.Success(sonuc);

        var kategoriler = await sfDb.ChannelCategories.AsNoTracking()
            .Where(c => c.FirmPlatformId == request.FirmPlatformId && c.Status == "published")
            .Select(c => new Kategori(
                c.Id, c.ParentId, c.Slug, c.NameI18n, c.FillType, c.FilterDef, c.SortOrder))
            .ToListAsync(ct);
        if (kategoriler.Count == 0)
            return Result.Success(sonuc);

        var grupByUrun = await catDb.Products.AsNoTracking()
            .Where(p => urunIdler.Contains(p.Id))
            .Select(p => new { p.Id, p.ProductGroupId })
            .ToDictionaryAsync(x => x.Id, x => x.ProductGroupId, ct);

        var degerSatirlari = await catDb.ProductAttributes.AsNoTracking()
            .Where(a => urunIdler.Contains(a.ProductId) && a.AttributeValueId != null)
            .Select(a => new { a.ProductId, a.AttributeTypeId, ValueId = a.AttributeValueId!.Value })
            .ToListAsync(ct);
        var degerlerByUrun = degerSatirlari
            .GroupBy(a => a.ProductId)
            .ToDictionary(g => g.Key, g => g
                .GroupBy(a => a.AttributeTypeId)
                .ToDictionary(t => t.Key, t => t.Select(x => x.ValueId).ToHashSet()));

        var manuelSatirlar = await sfDb.ChannelCategoryProducts.AsNoTracking()
            .Where(cp => urunIdler.Contains(cp.ProductId))
            .Select(cp => new { cp.ProductId, cp.ChannelCategoryId, cp.IsExcluded })
            .ToListAsync(ct);
        var dahilByUrun = manuelSatirlar.Where(r => !r.IsExcluded)
            .GroupBy(r => r.ProductId).ToDictionary(g => g.Key, g => g.Select(r => r.ChannelCategoryId).ToHashSet());
        var haricByUrun = manuelSatirlar.Where(r => r.IsExcluded)
            .GroupBy(r => r.ProductId).ToDictionary(g => g.Key, g => g.Select(r => r.ChannelCategoryId).ToHashSet());

        // Kategori kuralları bir kez ayrıştırılır
        var kurallar = kategoriler
            .Where(c => c.FillType is "filter" or "mixed")
            .Select(c => (Kategori: c, Rules: CategoryFilterRules.From(c.FilterDef)))
            .Where(x => x.Rules is not null
                        && (x.Rules.ProductGroupIds is { Count: > 0 } || x.Rules.AttributeFilters is { Count: > 0 }))
            .ToList();

        var idIleKategori = kategoriler.ToDictionary(c => c.Id);
        var derinlikCache = new Dictionary<Guid, int>();
        int Derinlik(Kategori k)
        {
            if (derinlikCache.TryGetValue(k.Id, out var d)) return d;
            var derinlik = 0; var m = k;
            while (m.ParentId is { } pid && idIleKategori.TryGetValue(pid, out var parent)) { derinlik++; m = parent; }
            return derinlikCache[k.Id] = derinlik;
        }

        foreach (var urunId in urunIdler)
        {
            if (!grupByUrun.TryGetValue(urunId, out var grupId)) continue;
            var degerlerTipe = degerlerByUrun.GetValueOrDefault(urunId) ?? new();
            var dahil = dahilByUrun.GetValueOrDefault(urunId);
            var haric = haricByUrun.GetValueOrDefault(urunId);

            var adaylar = new List<Kategori>();
            foreach (var (kat, rules) in kurallar)
            {
                if (haric is not null && haric.Contains(kat.Id)) continue;
                if (rules!.ProductGroupIds is { Count: > 0 } && !rules.ProductGroupIds.Contains(grupId)) continue;
                var uyar = true;
                if (rules.AttributeFilters is { Count: > 0 })
                {
                    foreach (var af in rules.AttributeFilters)
                    {
                        if (!degerlerTipe.TryGetValue(af.AttributeTypeId, out var sahip) || !af.ValueIds.Any(sahip.Contains))
                        { uyar = false; break; }
                    }
                }
                if (uyar) adaylar.Add(kat);
            }
            if (dahil is not null)
                foreach (var id in dahil)
                    if ((haric is null || !haric.Contains(id)) && idIleKategori.TryGetValue(id, out var k) && !adaylar.Contains(k))
                        adaylar.Add(k);

            if (adaylar.Count == 0) continue;
            var yaprak = adaylar.OrderByDescending(Derinlik).ThenBy(c => c.SortOrder).First();
            sonuc.Add(new ProductLeafCategoryDto(urunId, yaprak.Id, yaprak.Slug, yaprak.NameI18n));
        }

        return Result.Success(sonuc);
    }
}
