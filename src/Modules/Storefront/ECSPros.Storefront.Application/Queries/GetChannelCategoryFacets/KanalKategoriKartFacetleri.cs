using ECSPros.Catalog.Application.Queries.GetStoreFacets;
using ECSPros.Catalog.Application.Services;
using Microsoft.EntityFrameworkCore;
using KartPair = ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts.GetChannelCategoryProductsQueryHandler.KartPair;
using UrunBilgi = ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts.GetChannelCategoryProductsQueryHandler.UrunBilgi;

namespace ECSPros.Storefront.Application.Queries.GetChannelCategoryFacets;

/// <summary>
/// Kategori (renk modu) filtre sayıları — KART biriminde (2026-08-26 kullanıcı kararı:
/// "hem filtre hem liste başlıklarında renk sayıları görünsün"). Önceden facet ÜRÜN sayar,
/// liste başlığı KART sayardı ("8950 ürün / Kadın (2604) / seçince 5952" tutarsızlığı).
/// Sayım listenin kart evreni (KartEvreniKurAsync) üzerinden yapılır; bir değerin sayısı,
/// YALNIZ o değer seçilseydi listede kalacak kart adedidir (liste seçme kuralıyla birebir:
/// değer ürün seviyesinde ürüne, varyant seviyesinde kartın kendi varyantlarına bakar).
/// Seçim-duyarlı kurallar GetStoreFacetsQueryHandler.BuildFacetsWithSelections ile aynı:
/// grup kendi seçimini dışlar, fiyat sınırları fiyat filtresi hariç kümeden, kategori
/// sanal grubu özellik/fiyat/kategori seçiminden bağımsız TÜM tabandan (kart ağırlıklı).
/// </summary>
public static class KanalKategoriKartFacetleri
{
    public static async Task<StoreFacetsDto> KurAsync(
        ICatalogDbContext db,
        IReadOnlyList<KartPair> pairs,
        IReadOnlyDictionary<Guid, UrunBilgi> productInfo,
        List<Guid>? selectedValueIds,
        decimal? priceMin,
        decimal? priceMax,
        IReadOnlyDictionary<Guid, Guid>? productCategoryMap,
        List<Guid>? selectedCategoryIds,
        CancellationToken ct)
    {
        var secili = (selectedValueIds ?? []).Distinct().ToList();

        // Kategori sanal grubu: kısıtsız TÜM tabandan, kart ağırlıklı (bkz. özet).
        Dictionary<Guid, int>? kategoriSayimi = null;
        if (productCategoryMap is not null)
        {
            kategoriSayimi = new();
            foreach (var pair in pairs)
                if (productCategoryMap.TryGetValue(pair.ProductId, out var kat))
                    kategoriSayimi[kat] = kategoriSayimi.GetValueOrDefault(kat) + 1;
        }

        // Kategori kısıtı: diğer gruplar/fiyat bu kümeden hesaplanır.
        var seciliKategoriler = selectedCategoryIds is { Count: > 0 } && productCategoryMap is not null
            ? selectedCategoryIds.ToHashSet() : null;
        var taban = seciliKategoriler is null
            ? pairs.ToList()
            : pairs.Where(p => productCategoryMap!.TryGetValue(p.ProductId, out var k)
                            && seciliKategoriler.Contains(k)).ToList();
        if (taban.Count == 0)
            return new StoreFacetsDto(0, 0, new(), kategoriSayimi);

        var productIds = taban.Select(p => p.ProductId).Distinct().ToList();

        // Filtreye giren (UseInFilter) tüm değer satırları — varyant + ürün seviyesi
        var varRows = await db.ProductVariantAttributes.AsNoTracking()
            .Where(va => va.Variant.IsActive && va.AttributeType.UseInFilter
                      && productIds.Contains(va.Variant.ProductId))
            .Select(va => new { va.VariantId, va.AttributeTypeId, va.AttributeValueId })
            .ToListAsync(ct);
        var prodRows = await db.ProductAttributes.AsNoTracking()
            .Where(pa => pa.AttributeValueId != null && pa.AttributeType.UseInFilter
                      && productIds.Contains(pa.ProductId))
            .Select(pa => new { pa.ProductId, pa.AttributeTypeId, AttributeValueId = pa.AttributeValueId!.Value })
            .ToListAsync(ct);

        var degerByVariant = varRows.GroupBy(r => r.VariantId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.AttributeValueId).ToHashSet());
        var degerByUrun = prodRows.GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.AttributeValueId).ToHashSet());
        var tipByDeger = new Dictionary<Guid, Guid>();
        foreach (var r in varRows) tipByDeger[r.AttributeValueId] = r.AttributeTypeId;
        foreach (var r in prodRows) tipByDeger[r.AttributeValueId] = r.AttributeTypeId;

        // Seçili değerlerin tipleri (bu kümede hiç geçmeyen seçili değer olabilir)
        var eksikSecili = secili.Where(v => !tipByDeger.ContainsKey(v)).ToList();
        if (eksikSecili.Count > 0)
            foreach (var v in await db.AttributeValues.AsNoTracking()
                         .Where(av => eksikSecili.Contains(av.Id))
                         .Select(av => new { av.Id, av.AttributeTypeId }).ToListAsync(ct))
                tipByDeger[v.Id] = v.AttributeTypeId;
        var tipGruplari = secili.Where(tipByDeger.ContainsKey)
            .GroupBy(v => tipByDeger[v])
            .ToDictionary(g => g.Key, g => g.ToHashSet());

        bool KartSaglar(KartPair pair, IReadOnlyCollection<HashSet<Guid>> gruplar)
        {
            if (gruplar.Count == 0) return true;
            degerByUrun.TryGetValue(pair.ProductId, out var urunSahip);
            return gruplar.All(g =>
                (urunSahip is not null && urunSahip.Overlaps(g))
                || pair.VariantIds.Any(vid => degerByVariant.TryGetValue(vid, out var sv) && sv.Overlaps(g)));
        }
        decimal Fiyat(KartPair pair) => pair.Price > 0 ? pair.Price
            : productInfo.TryGetValue(pair.ProductId, out var pi) ? pi.BasePrice : 0;
        bool FiyatUyar(KartPair pair)
        {
            if (!priceMin.HasValue && !priceMax.HasValue) return true;
            var f = Fiyat(pair);
            return (!priceMin.HasValue || f >= priceMin.Value) && (!priceMax.HasValue || f <= priceMax.Value);
        }

        // Kartın taşıdığı değerler (ürün seviyesi + kendi varyantları) — sayım çekirdeği
        Dictionary<Guid, Dictionary<Guid, int>> Sayim(List<KartPair> kume)   // tip → (değer → kart)
        {
            var sonuc = new Dictionary<Guid, Dictionary<Guid, int>>();
            var kartDegerleri = new HashSet<Guid>();
            foreach (var pair in kume)
            {
                kartDegerleri.Clear();
                if (degerByUrun.TryGetValue(pair.ProductId, out var u)) kartDegerleri.UnionWith(u);
                foreach (var vid in pair.VariantIds)
                    if (degerByVariant.TryGetValue(vid, out var sv)) kartDegerleri.UnionWith(sv);
                foreach (var d in kartDegerleri)
                {
                    var tip = tipByDeger[d];
                    if (!sonuc.TryGetValue(tip, out var m)) sonuc[tip] = m = new();
                    m[d] = m.GetValueOrDefault(d) + 1;
                }
            }
            return sonuc;
        }

        var tumGruplar = tipGruplari.Values.ToList();
        var secimUygulanmis = taban.Where(p => KartSaglar(p, tumGruplar)).ToList();

        // Fiyat sınırları: fiyat filtresi HARİÇ, tüm grup seçimleri uygulanmış kümeden
        decimal fMin = 0, fMax = 0;
        foreach (var pair in secimUygulanmis)
        {
            var f = Fiyat(pair);
            if (f <= 0) continue;
            if (fMin == 0 || f < fMin) fMin = f;
            if (f > fMax) fMax = f;
        }

        // Seçimsiz gruplar: tüm seçimler + fiyat uygulanmış kümeden
        var sayimlar = Sayim(secimUygulanmis.Where(FiyatUyar).ToList());
        // Seçimli gruplar: kendi grubu HARİÇ diğer seçimler + fiyat uygulanmış kümeden
        foreach (var tipId in tipGruplari.Keys)
        {
            var digerleri = tipGruplari.Where(g => g.Key != tipId).Select(g => g.Value).ToList();
            var kume = taban.Where(p => KartSaglar(p, digerleri) && FiyatUyar(p)).ToList();
            sayimlar[tipId] = Sayim(kume).GetValueOrDefault(tipId) ?? new Dictionary<Guid, int>();
        }

        // Meta (ad/sıra/hex) + DTO — GetStoreFacetsQueryHandler.BuildFacets ile aynı kurallar
        var typeIds = sayimlar.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key).Distinct().ToList();
        var valueIds = sayimlar.Values.SelectMany(m => m.Keys).Distinct().ToList();
        if (typeIds.Count == 0)
            return new StoreFacetsDto(fMin, fMax, new(), kategoriSayimi);

        var types = await db.AttributeTypes.AsNoTracking()
            .Where(t => typeIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Code, t.NameI18n, t.SortOrder })
            .ToListAsync(ct);
        var values = await db.AttributeValues.AsNoTracking()
            .Where(v => valueIds.Contains(v.Id))
            .Select(v => new { v.Id, v.NameI18n, v.SortOrder, v.HexCode })
            .ToListAsync(ct);
        var typeById = types.ToDictionary(t => t.Id);
        var valueById = values.ToDictionary(v => v.Id);

        var attributes = sayimlar
            .Where(kv => kv.Value.Count > 0 && typeById.ContainsKey(kv.Key))
            .OrderBy(kv => typeById[kv.Key].SortOrder)
            .Select(kv =>
            {
                var t = typeById[kv.Key];
                return new AttributeFacetDto(
                    t.Code,
                    t.NameI18n,
                    t.Code is "filtre_rengi" or "renk",
                    kv.Value
                        .Where(d => valueById.ContainsKey(d.Key))
                        .OrderBy(d => valueById[d.Key].SortOrder)
                        .Select(d =>
                        {
                            var v = valueById[d.Key];
                            return new AttributeFacetValueDto(d.Key, v.NameI18n, v.HexCode, d.Value);
                        })
                        .ToList());
            })
            .ToList();

        return GetStoreFacetsQueryHandler.TekSecenekliGruplariAyikla(
            new StoreFacetsDto(fMin, fMax, attributes, kategoriSayimi), secili);
    }
}
