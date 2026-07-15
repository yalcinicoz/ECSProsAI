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
    DateTime? OutOfStockSince = null) : IRequest<Result<StoreFacetsDto>>;

public record StoreFacetsDto(
    decimal PriceMin,
    decimal PriceMax,
    List<AttributeFacetDto> Attributes);

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
    IChannelProductFlagService flagService)
    : IRequestHandler<GetStoreFacetsQuery, Result<StoreFacetsDto>>
{
    // Tüm-katalog facet'i sorgu başına ~4 sn süren bir aggregation; katalog nadiren
    // değiştiği için süreç-içi cache yeterli (Redis bu ortamda kullanılamıyor —
    // bkz. PROGRESS.md 2026-07-06 Redis notu). Arama filtreli istekler cache'lenmez.
    // Cache anahtarı stok görünürlük paramlarını + platformu içerir (kanal seçimi/durdurma
    // deny-set'i platform bazlı — M2/M3).
    private static string AllKey(Guid platformId, bool showOos, DateTime? since) =>
        $"store:facets:all:v3:{platformId}:{showOos}:{since:yyyyMMdd}";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public async Task<Result<StoreFacetsDto>> Handle(GetStoreFacetsQuery request, CancellationToken ct)
    {
        var hasSearch = !string.IsNullOrWhiteSpace(request.Search);
        var allKey = AllKey(request.FirmPlatformId, request.ShowOutOfStock, request.OutOfStockSince);

        if (!hasSearch && memoryCache.TryGetValue(allKey, out StoreFacetsDto? cached) && cached is not null)
            return Result.Success(cached);

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

        if (hasSearch)
        {
            // GetStoreProducts ile aynı eşleşme kuralı (kod VEYA Türkçe ad) — arama sonuç
            // sayfasının facet'leri grid'le tutarlı kalsın.
            var s = request.Search!.Trim().ToLower();
            q = q.Where(p => p.Code.ToLower().Contains(s)
                          || PgJsonFunctions.JsonText(p.NameI18n, "tr")!.ToLower().Contains(s));
        }

        // Ürün id'leri belleğe çekilmez — alt sorgu olarak aggregation'a gömülür
        // (tüm katalogda ~90K id materialize etmek hem yavaş hem gereksizdi).
        var result = await BuildFacets(db, q.Select(p => p.Id), ct);

        if (!hasSearch && result.IsSuccess)
            memoryCache.Set(allKey, result.Value, CacheTtl);

        return result;
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
        // "renk" (ürüne özel serbest metin renk adı, binlerce farklı değer) listeleme filtresi
        // olarak sunulmaz — renk filtresi "filtre_rengi" (kürasyonlu ~25 renk grubu, hex kodlu)
        // üzerinden verilir; "renk" ürün kartı/detayında ayrı bir alan olarak kalır.
        var counts = await db.ProductVariantAttributes
            .AsNoTracking()
            .Where(va => va.Variant.IsActive
                && va.AttributeType.Code != "renk"
                && productIds.Contains(va.Variant.ProductId))
            .Select(va => new { va.AttributeTypeId, va.AttributeValueId, va.Variant.ProductId })
            .Distinct()
            .GroupBy(x => new { x.AttributeTypeId, x.AttributeValueId })
            .Select(g => new { g.Key.AttributeTypeId, g.Key.AttributeValueId, ProductCount = g.Count() })
            .ToListAsync(ct);

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
                    t.Code == "filtre_rengi",
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
