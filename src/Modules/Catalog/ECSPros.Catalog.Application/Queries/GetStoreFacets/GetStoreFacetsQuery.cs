using ECSPros.Shared.Contracts;
using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Catalog.Application.Queries.GetStoreFacets;

public record GetStoreFacetsQuery(
    Guid FirmPlatformId,
    string? Search = null,
    // Stok görünürlüğü — arama listesiyle facet sayıları tutarlı kalsın (2026-07-14).
    bool ShowOutOfStock = false,
    DateTime? OutOfStockSince = null,
    // 2026-07-17: seçim-duyarlı facet — seçili değerler + fiyat aralığı verilirse her
    // filtre grubu "diğer grupların seçimleri uygulanmış" ürün kümesinden hesaplanır
    // (grup kendi seçimini dışlar → son kullanılan grubun seçenekleri korunur).
    List<Guid>? SelectedValueIds = null,
    decimal? PriceMin = null,
    decimal? PriceMax = null,
    // 2026-08-15: sabit kod listesi (görsel arama sonucu / benzer ürünler) — facet'ler
    // yalnız bu ürünlerden hesaplanır (cache'lenmez; Search ile birlikte kullanılmaz).
    List<string>? ProductCodes = null,
    // 2026-08-15: KATEGORİ sanal facet grubu — ürün→yaprak kanal kategorisi haritası (Api'de
    // platform başına 15 dk cache'li) verilirse CategoryCounts döner; SelectedCategoryIds
    // seçili kategori(ler)dir: diğer gruplar bu kategorilere kısıtlı kümeden, kategori
    // grubunun kendisi ise kategori kısıtı OLMADAN (klasik facet kuralı) hesaplanır.
    IReadOnlyDictionary<Guid, Guid>? ProductCategoryMap = null,
    List<Guid>? SelectedCategoryIds = null) : IRequest<Result<StoreFacetsDto>>;

public record StoreFacetsDto(
    decimal PriceMin,
    decimal PriceMax,
    List<AttributeFacetDto> Attributes,
    // 2026-08-15: kategori sanal grubu — yaprak kategori id → ürün sayısı (harita verildiyse)
    Dictionary<Guid, int>? CategoryCounts = null);

public record AttributeFacetDto(
    string TypeCode,
    Dictionary<string, string> TypeNameI18n,
    bool IsColorType,
    List<AttributeFacetValueDto> Values);

public record AttributeFacetValueDto(
    Guid ValueId,
    Dictionary<string, string> NameI18n,
    string? HexCode,
    int ProductCount);

public class GetStoreFacetsQueryHandler(
    ICatalogDbContext db, IInStockProductProvider inStock, IMemoryCache memoryCache,
    IChannelProductFlagService flagService,
    ECSPros.Shared.Contracts.IEffectivePriceProvider effectivePrices)
    : IRequestHandler<GetStoreFacetsQuery, Result<StoreFacetsDto>>
{
    // Tüm-katalog facet'i sorgu başına ~4 sn süren bir aggregation; katalog nadiren
    // değiştiği için süreç-içi cache yeterli (Redis bu ortamda kullanılamıyor —
    // bkz. PROGRESS.md 2026-07-06 Redis notu). Arama filtreli istekler cache'lenmez.
    // Cache anahtarı stok görünürlük paramlarını + platformu içerir (kanal seçimi/durdurma
    // deny-set'i platform bazlı — M2/M3).
    // v6: facet'e girecek tipler AttributeType.UseInFilter bayrağından seçilir (2026-07-17)
    // v7: tek seçenekli filtre grupları panelden düşürülür (2026-07-17)
    private static string AllKey(Guid platformId, bool showOos, DateTime? since) =>
        $"store:facets:all:v7:{platformId}:{showOos}:{since:yyyyMMdd}";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public async Task<Result<StoreFacetsDto>> Handle(GetStoreFacetsQuery request, CancellationToken ct)
    {
        var hasCodes = request.ProductCodes is { Count: > 0 };
        var hasSearch = !string.IsNullOrWhiteSpace(request.Search) || hasCodes; // kod listesi de cache dışı
        var harita = request.ProductCategoryMap;
        var seciliKategoriler = request.SelectedCategoryIds is { Count: > 0 } sk ? sk.Distinct().ToList() : [];
        var secimVar = request.SelectedValueIds is { Count: > 0 } || request.PriceMin.HasValue
                       || request.PriceMax.HasValue || seciliKategoriler.Count > 0;
        var allKey = AllKey(request.FirmPlatformId, request.ShowOutOfStock, request.OutOfStockSince);
        var allIdsKey = allKey + ":ids";

        if (!hasSearch && !secimVar
            && memoryCache.TryGetValue(allKey, out StoreFacetsDto? cached) && cached is not null)
        {
            if (harita is null) return Result.Success(cached);
            // Kategori sayımı: taban id'ler ayrı cache'te (materialize ucuz, harita 15 dk'lık)
            if (memoryCache.TryGetValue(allIdsKey, out List<Guid>? cachedIds) && cachedIds is not null)
                return Result.Success(cached with { CategoryCounts = KategoriSayimi(cachedIds, harita) });
        }

        // Stok görünürlüğü: arama grid'iyle aynı kural (stoğu biten, kanal açık VE CreatedAt>=eşik
        // değilse facet'lerden de çıkar).
        var inStockIds = await inStock.GetInStockProductIdsAsync(ct);
        // Kanal seçimi/durdurma (M2/M3): kanaldan çıkarılan/durdurulan ürünü facet'ten de çıkar.
        var kanalDisi = await flagService.GetChannelExcludedProductIdsAsync(request.FirmPlatformId, ct);
        var showOos = request.ShowOutOfStock;
        var oosSince = request.OutOfStockSince;
        var q = db.Products
            .AsNoTracking()
            .Where(p => p.IsSaleOpen && db.ProductImages.Any(img => img.ProductId == p.Id)
                     && !kanalDisi.Contains(p.Id))
            .Where(p => inStockIds.Contains(p.Id) || (showOos && (oosSince == null || p.CreatedAt >= oosSince)));

        if (hasCodes)
        {
            var kodlar = request.ProductCodes!.Select(k => k.ToUpperInvariant()).Distinct().ToList();
            q = q.Where(p => kodlar.Contains(p.Code.ToUpper()));
        }
        else if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // GetStoreProducts ile aynı eşleşme kuralı (kod VEYA Türkçe ad) — arama sonuç
            // sayfasının facet'leri grid'le tutarlı kalsın.
            var s = request.Search!.Trim().ToLower();
            q = q.Where(p => p.Code.ToLower().Contains(s)
                          || PgJsonFunctions.JsonText(p.NameI18n, "tr")!.ToLower().Contains(s));
        }

        // 2026-07-17: seçim/fiyat filtresi varsa seçim-duyarlı hesap (cache'lenmez);
        // arama listesi ürün seviyesinde eşleştiğinden sameVariant=false.
        // 2026-08-15: arama/kod listesi de (kategori sayımı için) materialize edilir.
        if (secimVar || hasSearch)
        {
            var baseIds = await q.Select(p => p.Id).ToListAsync(ct);
            // B10 (2026-08-27): fiyat bağlamı efektif fiyatla — liste filtresiyle aynı küme
            var efektif = (request.PriceMin.HasValue || request.PriceMax.HasValue)
                ? await effectivePrices.GetMinEffectivePricesAsync(request.FirmPlatformId, ct)
                : null;
            return await BuildFacetsWithSelections(
                db, baseIds, request.SelectedValueIds, request.PriceMin, request.PriceMax,
                sameVariant: false, ct, harita, seciliKategoriler, efektif);
        }

        // Ürün id'leri belleğe çekilmez — alt sorgu olarak aggregation'a gömülür
        // (tüm katalogda ~90K id materialize etmek hem yavaş hem gereksizdi).
        var result = await BuildFacets(db, q.Select(p => p.Id), ct);
        if (result.IsSuccess)
            result = Result.Success(TekSecenekliGruplariAyikla(result.Value!));

        if (result.IsSuccess)
        {
            memoryCache.Set(allKey, result.Value, CacheTtl);
            if (harita is not null)
            {
                // Kategori sayımı için taban id'ler (yalnız id kolonu; ~30K guid, ucuz) — 15 dk
                var ids = await q.Select(p => p.Id).ToListAsync(ct);
                memoryCache.Set(allIdsKey, ids, CacheTtl);
                result = Result.Success(result.Value! with { CategoryCounts = KategoriSayimi(ids, harita) });
            }
        }

        return result;
    }

    /// <summary>Kategori sanal grubu sayımı: verilen ürün kümesindeki ürünlerin yaprak
    /// kategorilerine göre ürün sayısı (haritada olmayan ürün sayılmaz).</summary>
    public static Dictionary<Guid, int> KategoriSayimi(IEnumerable<Guid> productIds, IReadOnlyDictionary<Guid, Guid> harita)
    {
        var sayim = new Dictionary<Guid, int>();
        foreach (var id in productIds)
            if (harita.TryGetValue(id, out var kat))
                sayim[kat] = sayim.GetValueOrDefault(kat) + 1;
        return sayim;
    }

    /// <summary>Tek seçeneği kalan filtre grubunu panelden düşürür (2026-07-17 kullanıcı
    /// kuralı: hiç değer içermeyen grup gibi tek seçenekli grup da filtre alanına konmaz —
    /// tek seçenek ayırt ediciliği olmayan gereksiz kalabalıktır). İstisna: kullanıcının
    /// SEÇİLİ değerini içeren grup tek seçeneğe düşse bile korunur; yoksa seçimi panelden
    /// kaldırma yolu kaybolur.</summary>
    public static StoreFacetsDto TekSecenekliGruplariAyikla(
        StoreFacetsDto dto, ICollection<Guid>? seciliDegerIds = null)
    {
        var secili = seciliDegerIds is { Count: > 0 } ? seciliDegerIds.ToHashSet() : null;
        var kalan = dto.Attributes
            .Where(a => a.Values.Count >= 2
                     || (secili is not null && a.Values.Any(v => secili.Contains(v.ValueId))))
            .ToList();
        return kalan.Count == dto.Attributes.Count ? dto : dto with { Attributes = kalan };
    }

    /// <summary>2026-07-17: seçim-duyarlı facet — klasik çok-seçimli facet kuralı: her
    /// filtre grubunun seçenekleri, DİĞER grupların seçimleri + fiyat aralığı uygulanmış
    /// ürün kümesinden türetilir (grup kendi seçimini dışlar; böylece son kullanılan grubun
    /// seçenekleri korunur ve sunulan her seçenek en az 1 ürünle sonuçlanır). sameVariant:
    /// kategori listesi grupları AYNI varyantta arar, arama listesi ürün seviyesinde.</summary>
    public static async Task<Result<StoreFacetsDto>> BuildFacetsWithSelections(
        ICatalogDbContext db,
        List<Guid> baseProductIds,
        List<Guid>? selectedValueIds,
        decimal? priceMin,
        decimal? priceMax,
        bool sameVariant,
        CancellationToken ct,
        // 2026-08-15: kategori sanal grubu (bkz. GetStoreFacetsQuery.ProductCategoryMap)
        IReadOnlyDictionary<Guid, Guid>? productCategoryMap = null,
        List<Guid>? selectedCategoryIds = null,
        // B10 kapanışı (2026-08-27): verilirse fiyat filtresi efektif (kanal) fiyatla uygulanır
        // (kartla aynı); null → eski BasePrice davranışı (Storefront fallback çağıranları).
        IReadOnlyDictionary<Guid, decimal>? efektifFiyatlar = null)
    {
        var secili = (selectedValueIds ?? []).Distinct().ToList();
        var seciliKategoriler = selectedCategoryIds is { Count: > 0 } && productCategoryMap is not null
            ? selectedCategoryIds.ToHashSet() : null;
        // Kategori kısıtı: seçili kategori varsa diğer gruplar/fiyat/liste bu kümeden hesaplanır
        var tumTaban = baseProductIds; // kategori kısıtı UYGULANMAMIŞ (kategori grubunun kendi sayımı için)
        if (seciliKategoriler is not null)
            baseProductIds = baseProductIds
                .Where(id => productCategoryMap!.TryGetValue(id, out var k) && seciliKategoriler.Contains(k))
                .ToList();

        if (secili.Count == 0 && !priceMin.HasValue && !priceMax.HasValue)
        {
            var duz = await BuildFacets(db, baseProductIds, ct);
            if (duz.IsFailure) return duz;
            var duzDto = TekSecenekliGruplariAyikla(duz.Value!);
            if (productCategoryMap is not null)
                duzDto = duzDto with { CategoryCounts = KategoriSayimi(tumTaban, productCategoryMap) };
            return Result.Success(duzDto);
        }

        if (baseProductIds.Count == 0)
            return Result.Success(new StoreFacetsDto(0, 0, new(),
                productCategoryMap is null ? null : new Dictionary<Guid, int>()));

        // Seçili değerler tip (grup) bazında
        var tipGruplari = (await db.AttributeValues.AsNoTracking()
                .Where(v => secili.Contains(v.Id))
                .Select(v => new { v.Id, v.AttributeTypeId })
                .ToListAsync(ct))
            .GroupBy(v => v.AttributeTypeId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToHashSet());

        // Seçili değerlerin ürün/varyant eşleşmeleri tek sorguda belleğe alınır —
        // grup-hariç alt kümeler bellekten türetilir (grup sayısı kadar DB taraması yerine).
        var urunVaryantDegerleri = new Dictionary<Guid, Dictionary<Guid, HashSet<Guid>>>();
        // 2026-07-17: ürün SEVİYESİ değerler (product_attributes) — ürün geneline sayılır
        var urunSeviyesiDegerleri = new Dictionary<Guid, HashSet<Guid>>();
        if (tipGruplari.Count > 0)
        {
            var satirlar = await db.ProductVariantAttributes.AsNoTracking()
                .Where(va => secili.Contains(va.AttributeValueId)
                          && baseProductIds.Contains(va.Variant.ProductId)
                          && va.Variant.IsActive)
                .Select(va => new { va.Variant.ProductId, va.VariantId, va.AttributeValueId })
                .ToListAsync(ct);
            foreach (var satir in satirlar)
            {
                if (!urunVaryantDegerleri.TryGetValue(satir.ProductId, out var varyantlar))
                    urunVaryantDegerleri[satir.ProductId] = varyantlar = new();
                if (!varyantlar.TryGetValue(satir.VariantId, out var degerler))
                    varyantlar[satir.VariantId] = degerler = new();
                degerler.Add(satir.AttributeValueId);
            }

            var urunSatirlari = await db.ProductAttributes.AsNoTracking()
                .Where(pa => pa.AttributeValueId != null
                          && secili.Contains(pa.AttributeValueId.Value)
                          && baseProductIds.Contains(pa.ProductId))
                .Select(pa => new { pa.ProductId, AttributeValueId = pa.AttributeValueId!.Value })
                .ToListAsync(ct);
            foreach (var satir in urunSatirlari)
            {
                if (!urunSeviyesiDegerleri.TryGetValue(satir.ProductId, out var degerler))
                    urunSeviyesiDegerleri[satir.ProductId] = degerler = new();
                degerler.Add(satir.AttributeValueId);
            }
        }

        bool UrunGruplariSaglarMi(Guid productId, List<HashSet<Guid>> gruplar)
        {
            if (gruplar.Count == 0) return true;
            urunVaryantDegerleri.TryGetValue(productId, out var varyantlar);
            urunSeviyesiDegerleri.TryGetValue(productId, out var urunSahip);
            if (varyantlar is null && urunSahip is null) return false;
            bool UrunSeviyesindeVar(HashSet<Guid> grup) => urunSahip is not null && urunSahip.Overlaps(grup);
            if (sameVariant)
            {
                if (varyantlar is null) return gruplar.All(UrunSeviyesindeVar);
                return varyantlar.Values.Any(sahip => gruplar.All(g => sahip.Overlaps(g) || UrunSeviyesindeVar(g)))
                       || gruplar.All(UrunSeviyesindeVar);
            }
            var tumu = varyantlar is null ? new HashSet<Guid>() : varyantlar.Values.SelectMany(s => s).ToHashSet();
            return gruplar.All(g => tumu.Overlaps(g) || UrunSeviyesindeVar(g));
        }

        async Task<List<Guid>> FiyatUygula(List<Guid> ids)
        {
            if ((!priceMin.HasValue && !priceMax.HasValue) || ids.Count == 0) return ids;
            var min = priceMin ?? 0;
            var max = priceMax ?? decimal.MaxValue;
            if (efektifFiyatlar is not null)
            {
                // B10: kartta gösterilen (efektif) fiyatla — liste filtresiyle birebir aynı küme
                var eksikler = new List<Guid>();
                var sonuc = new List<Guid>();
                foreach (var id in ids)
                {
                    if (efektifFiyatlar.TryGetValue(id, out var f))
                    { if (f >= min && f <= max) sonuc.Add(id); }
                    else eksikler.Add(id);
                }
                if (eksikler.Count > 0)
                    sonuc.AddRange(await db.Products.AsNoTracking()
                        .Where(p => eksikler.Contains(p.Id) && p.BasePrice >= min && p.BasePrice <= max)
                        .Select(p => p.Id).ToListAsync(ct));
                return sonuc;
            }
            return await db.Products.AsNoTracking()
                .Where(p => ids.Contains(p.Id)
                    && (p.Variants.Any(v => v.IsActive && v.BasePrice > 0 && v.BasePrice >= min && v.BasePrice <= max)
                        || (p.BasePrice >= min && p.BasePrice <= max
                            && !p.Variants.Any(v => v.IsActive && v.BasePrice > 0))))
                .Select(p => p.Id)
                .ToListAsync(ct);
        }

        var tumGruplar = tipGruplari.Values.ToList();
        var hepsiUygulanmis = baseProductIds.Where(id => UrunGruplariSaglarMi(id, tumGruplar)).ToList();

        // Fiyat aralığı sınırları: fiyat filtresi HARİÇ, tüm grup seçimleri uygulanmış küme
        var fiyatAgg = hepsiUygulanmis.Count == 0 ? null : await db.Products.AsNoTracking()
            .Where(p => hepsiUygulanmis.Contains(p.Id) && p.BasePrice > 0)
            .GroupBy(_ => 1)
            .Select(g => new { Min = g.Min(p => p.BasePrice), Max = g.Max(p => p.BasePrice) })
            .FirstOrDefaultAsync(ct);

        // Seçimsiz gruplar: tüm seçimler + fiyat uygulanmış kümeden
        var tamKume = await FiyatUygula(hepsiUygulanmis);
        var tamSonuc = await BuildFacets(db, tamKume, ct);
        if (tamSonuc.IsFailure)
            return tamSonuc;

        // Kategori sanal grubu (2026-08-23 düzeltmesi — kullanıcı: "Hacim seçince kategori seçenekleri
        // kayboluyor", 8d46a00 sonrası regresyon): kategori kutusu ÖZELLİK ve FİYAT seçiminden BAĞIMSIZ,
        // kategori kısıtı da uygulanmamış TÜM tabandan sayılır — kararlı kategori listesi (eski menü-kökü
        // kutusunun kararlılığı + yaprak kategoriler). Diğer gruplar/fiyat/liste kategori kısıtlı kümeden
        // hesaplanmaya devam eder. Özellik seçimine göre daralan kategori sayımı istenirse bu blok
        // yeniden UrunGruplariSaglarMi + FiyatUygula ile kurulur (8d46a00'daki sürüm).
        Dictionary<Guid, int>? kategoriSayimi = null;
        if (productCategoryMap is not null)
            kategoriSayimi = KategoriSayimi(tumTaban, productCategoryMap);

        var girdilerByCode = tamSonuc.Value!.Attributes.ToDictionary(a => a.TypeCode);

        // Seçimli gruplar: kendi grubu HARİÇ diğer seçimler + fiyat uygulanmış kümeden
        if (tipGruplari.Count > 0)
        {
            var tipMeta = await db.AttributeTypes.AsNoTracking()
                .Where(t => tipGruplari.Keys.Contains(t.Id))
                .Select(t => new { t.Id, t.Code })
                .ToListAsync(ct);
            foreach (var tip in tipMeta)
            {
                var digerleri = tipGruplari.Where(g => g.Key != tip.Id).Select(g => g.Value).ToList();
                var grupKumesi = await FiyatUygula(
                    baseProductIds.Where(id => UrunGruplariSaglarMi(id, digerleri)).ToList());
                var grupSonucu = await BuildFacets(db, grupKumesi, ct);
                if (grupSonucu.IsSuccess)
                {
                    var girdi = grupSonucu.Value!.Attributes.FirstOrDefault(a => a.TypeCode == tip.Code);
                    if (girdi is not null) { girdilerByCode[tip.Code] = girdi; }
                }
            }
        }

        // Tip sırasına göre birleştir
        var kodlar = girdilerByCode.Keys.ToList();
        var sira = await db.AttributeTypes.AsNoTracking()
            .Where(t => kodlar.Contains(t.Code))
            .Select(t => new { t.Code, t.SortOrder })
            .ToListAsync(ct);
        var siraByCode = sira.GroupBy(s => s.Code).ToDictionary(g => g.Key, g => g.Min(s => s.SortOrder));
        var birlesik = girdilerByCode.Values
            .OrderBy(a => siraByCode.GetValueOrDefault(a.TypeCode, int.MaxValue))
            .ToList();

        return Result.Success(TekSecenekliGruplariAyikla(
            new StoreFacetsDto(fiyatAgg?.Min ?? 0, fiyatAgg?.Max ?? 0, birlesik, kategoriSayimi), secili));
    }

    public static Task<Result<StoreFacetsDto>> BuildFacets(
        ICatalogDbContext db,
        List<Guid> productIds,
        CancellationToken ct)
    {
        if (productIds.Count == 0)
            return Task.FromResult(Result.Success(new StoreFacetsDto(0, 0, new())));

        return BuildFacets(db, db.Products.Where(p => productIds.Contains(p.Id)).Select(p => p.Id), ct);
    }

    public static async Task<Result<StoreFacetsDto>> BuildFacets(
        ICatalogDbContext db,
        IQueryable<Guid> productIds,
        CancellationToken ct)
    {
        // Fiyat aralığı — DB tarafında Min/Max (tüm fiyat listesi belleğe çekilmez)
        var priceAgg = await db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.BasePrice > 0)
            .GroupBy(_ => 1)
            .Select(g => new { Min = g.Min(p => p.BasePrice), Max = g.Max(p => p.BasePrice) })
            .FirstOrDefaultAsync(ct);

        var priceMin = priceAgg?.Min ?? 0;
        var priceMax = priceAgg?.Max ?? 0;

        // Facet sayıları DB'de toplanır: (tip, değer, ürün) tekilleştirilip gruplanır.
        // Eski sürüm tüm varyant-attribute satırlarını (3.7M+) belleğe çekiyordu — filtre_rengi
        // eşlemesinden sonra bu ~10 sn'ye çıkmıştı.
        // Filtreye girecek tipler AttributeType.UseInFilter bayrağıyla seçilir (2026-07-17) —
        // örn. "renk" (serbest metin, binlerce değer) bayrağı kapalıdır, renk filtresi
        // "filtre_rengi" (kürasyonlu ~25 renk grubu, hex kodlu) üzerinden verilir.
        var counts = await db.ProductVariantAttributes
            .AsNoTracking()
            .Where(va => va.Variant.IsActive
                && va.AttributeType.UseInFilter
                && productIds.Contains(va.Variant.ProductId))
            .Select(va => new { va.AttributeTypeId, va.AttributeValueId, va.Variant.ProductId })
            .Distinct()
            .GroupBy(x => new { x.AttributeTypeId, x.AttributeValueId })
            .Select(g => new { g.Key.AttributeTypeId, g.Key.AttributeValueId, ProductCount = g.Count() })
            .ToListAsync(ct);

        // 2026-07-17: ürün SEVİYESİ özellikler de filtreye dahil (kumaş türü, kalıp, desen,
        // cinsiyet, marka… product_attributes'ta yaşar — önceden yalnız varyant özellikleri
        // [beden/filtre_rengi] facet'e giriyordu). CustomValue-only satırlar (değersiz) atlanır.
        var urunSeviyesiSayilar = await db.ProductAttributes
            .AsNoTracking()
            .Where(pa => pa.AttributeValueId != null
                && pa.AttributeType.UseInFilter
                && productIds.Contains(pa.ProductId))
            .Select(pa => new { pa.AttributeTypeId, AttributeValueId = pa.AttributeValueId!.Value, pa.ProductId })
            .Distinct()
            .GroupBy(x => new { x.AttributeTypeId, x.AttributeValueId })
            .Select(g => new { g.Key.AttributeTypeId, g.Key.AttributeValueId, ProductCount = g.Count() })
            .ToListAsync(ct);

        if (urunSeviyesiSayilar.Count > 0)
        {
            // Aynı tip pratikte tek seviyede yaşar; nadir çakışmada sayılar toplanır.
            var birlesik = counts
                .Concat(urunSeviyesiSayilar)
                .GroupBy(c => new { c.AttributeTypeId, c.AttributeValueId })
                .Select(g => new { g.Key.AttributeTypeId, g.Key.AttributeValueId, ProductCount = g.Sum(x => x.ProductCount) })
                .ToList();
            counts = birlesik;
        }

        if (counts.Count == 0)
            return Result.Success(new StoreFacetsDto(priceMin, priceMax, new()));

        // Tip/değer meta verisi (ad, sıra, hex) — definition tabloları küçük, id listesi kısa
        var typeIds = counts.Select(c => c.AttributeTypeId).Distinct().ToList();
        var valueIds = counts.Select(c => c.AttributeValueId).Distinct().ToList();

        var types = await db.AttributeTypes
            .AsNoTracking()
            .Where(t => typeIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Code, t.NameI18n, t.SortOrder })
            .ToListAsync(ct);

        var values = await db.AttributeValues
            .AsNoTracking()
            .Where(v => valueIds.Contains(v.Id))
            .Select(v => new { v.Id, v.NameI18n, v.SortOrder, v.HexCode })
            .ToListAsync(ct);

        var typeById = types.ToDictionary(t => t.Id);
        var valueById = values.ToDictionary(v => v.Id);

        var attributes = counts
            .Where(c => typeById.ContainsKey(c.AttributeTypeId) && valueById.ContainsKey(c.AttributeValueId))
            .GroupBy(c => c.AttributeTypeId)
            .OrderBy(g => typeById[g.Key].SortOrder)
            .Select(g =>
            {
                var t = typeById[g.Key];
                return new AttributeFacetDto(
                    t.Code,
                    t.NameI18n,
                    t.Code is "filtre_rengi" or "renk",   // 2026-08-23: demo kanalında ham "renk" (hex'li) de swatch'lı renk grubu
                    g.OrderBy(c => valueById[c.AttributeValueId].SortOrder)
                        .Select(c =>
                        {
                            var v = valueById[c.AttributeValueId];
                            return new AttributeFacetValueDto(c.AttributeValueId, v.NameI18n, v.HexCode, c.ProductCount);
                        })
                        .ToList());
            })
            .ToList();

        return Result.Success(new StoreFacetsDto(priceMin, priceMax, attributes));
    }
}
