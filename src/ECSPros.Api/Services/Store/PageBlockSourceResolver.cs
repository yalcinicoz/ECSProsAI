using System.Text.Json;
using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using ECSPros.Order.Application.Queries.GetTopSellingVariants;
using ECSPros.Promotion.Application.Queries.GetActiveCampaignProductRefs;
using ECSPros.Shared.Contracts;
using ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts;
using ECSPros.Storefront.Application.Queries.GetShowcaseCollections;
using MediatR;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// G3: blok ConfigJson'ındaki ürün/koleksiyon kaynağı konfigürasyonu. Admin (G6) bu
/// sözleşmeyle yazar, resolver okur; snapshot içinde de aynen taşınır (spec: snapshot
/// ürün detayı değil kaynak+filtre konfigürasyonu tutar).
/// ConfigJson örneği: { "productSource": { "source": "best-sellers", "limit": 12 } }
/// </summary>
public record BlockProductSource(
    string Source,                       // new-arrivals | best-sellers | campaign | category | brand | manual
    Guid? CategoryId = null,             // category: ChannelCategoryId (admin seçiminden)
    Guid? BrandValueId = null,           // brand: definition attribute value id (marka)
    List<string>? ProductCodes = null,   // manual: sıra korunur
    List<Guid>? AttributeValueIds = null,
    decimal? PriceMin = null,
    decimal? PriceMax = null,
    int Limit = 12,
    string? Sort = null,                 // GetStoreProducts sözleşmesi: price_asc | price_desc | newest
    int? Days = null);                   // best-sellers penceresi (varsayılan 90)

public record BlockCollectionSource(
    int Limit = 10,
    string? Sort = null,                 // popular | null (en yeni)
    List<string>? ShareCodes = null);    // manuel seçim (sıra korunur)

public interface IPageBlockSourceResolver
{
    /// <summary>Kaynak konfigürasyonundan ürün kartları. page yalnız infinity devam yüklemesinde &gt; 1.</summary>
    Task<List<StoreProductDto>> ResolveProductsAsync(
        Guid firmPlatformId, BlockProductSource source, int page = 1, CancellationToken ct = default);

    Task<List<ShowcaseCollectionDto>> ResolveCollectionsAsync(
        Guid firmPlatformId, BlockCollectionSource source, CancellationToken ct = default);

    /// <summary>ConfigJson'dan kaynak düğümünü ayıklar; yoksa/bozuksa null (blok ürünsüz render edilir).</summary>
    BlockProductSource? ParseProductSource(string? configJson);
    BlockCollectionSource? ParseCollectionSource(string? configJson);
}

/// <summary>
/// G3 ürün kaynağı/filtre motoru — kompozisyon API katmanında (modüller birbirini
/// bilmez; varyant→ürün çözümü IProductService ile, E7/E8 deseni). Tüm kaynaklar tek
/// kart sözleşmesine (StoreProductDto) iner; kategori kaynağı kanal sorgusundan eşlenir
/// (kanal fiyatı + puanlar orada hazır). "Veri geldikçe açılır" kaynaklar (son
/// gezilenler E12, favoriler E5, önerilen) segment bağlamı istediğinden G-M2 ile gelir.
/// </summary>
public class PageBlockSourceResolver(IMediator mediator, IProductService productService)
    : IPageBlockSourceResolver
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<List<StoreProductDto>> ResolveProductsAsync(
        Guid firmPlatformId, BlockProductSource source, int page = 1, CancellationToken ct = default)
    {
        var limit = Math.Clamp(source.Limit, 1, 48);
        page = Math.Max(1, page);

        switch (source.Source)
        {
            case "new-arrivals":
                return await KartlariGetirAsync(firmPlatformId, source with { Sort = source.Sort ?? "newest" },
                    page, limit, ct: ct);

            case "manual":
            {
                if (source.ProductCodes is not { Count: > 0 }) return [];
                var kartlar = await KartlariGetirAsync(firmPlatformId, source, 1, limit,
                    productCodes: source.ProductCodes, ct: ct);
                return kartlar.OrderBy(k => source.ProductCodes.IndexOf(k.Code)).ToList();
            }

            case "brand":
            {
                if (source.BrandValueId is not { } marka) return [];
                var degerler = (source.AttributeValueIds ?? []).Append(marka).Distinct().ToList();
                return await KartlariGetirAsync(firmPlatformId, source, page, limit,
                    attributeValueIds: degerler, ct: ct);
            }

            case "category":
            {
                if (source.CategoryId is not { } kategoriId) return [];
                var sonuc = await mediator.Send(new GetChannelCategoryProductsQuery(
                    kategoriId, page, limit, null,
                    source.AttributeValueIds, source.PriceMin, source.PriceMax, source.Sort), ct);
                if (sonuc.IsFailure) return [];
                return sonuc.Value!.Items.Select(KanalKartinaCevir).ToList();
            }

            case "campaign":
            {
                var refs = await mediator.Send(new GetActiveCampaignProductRefsQuery(firmPlatformId), ct);
                if (refs.IsFailure) return [];
                var kodlar = await VaryantlariKodaCevirAsync(refs.Value!.VariantIds, ct);
                if (refs.Value.ProductIds.Count == 0 && kodlar.Count == 0) return [];
                return await KartlariGetirAsync(firmPlatformId, source, page, limit,
                    productCodes: kodlar.Count > 0 ? kodlar : null,
                    productIds: refs.Value.ProductIds.Count > 0 ? refs.Value.ProductIds : null, ct: ct);
            }

            case "best-sellers":
            {
                // Satış sırası korunmalı: sayfalama kod listesinin üzerinde yapılır
                var satislar = await mediator.Send(new GetTopSellingVariantsQuery(
                    firmPlatformId, source.Days ?? 90, limit * page * 3), ct);
                if (satislar.IsFailure || satislar.Value!.Count == 0) return [];
                var kodSirasi = await VaryantlariKodaCevirAsync(
                    satislar.Value.Select(s => s.VariantId).ToList(), ct);
                var sayfaKodlari = kodSirasi.Skip((page - 1) * limit).Take(limit).ToList();
                if (sayfaKodlari.Count == 0) return [];
                var kartlar = await KartlariGetirAsync(firmPlatformId, source, 1, limit,
                    productCodes: sayfaKodlari, ct: ct);
                return kartlar.OrderBy(k => sayfaKodlari.IndexOf(k.Code)).ToList();
            }

            default:
                return [];
        }
    }

    public async Task<List<ShowcaseCollectionDto>> ResolveCollectionsAsync(
        Guid firmPlatformId, BlockCollectionSource source, CancellationToken ct = default)
    {
        var sonuc = await mediator.Send(new GetShowcaseCollectionsQuery(
            firmPlatformId, source.Limit, source.Sort, source.ShareCodes), ct);
        return sonuc.IsSuccess ? sonuc.Value! : [];
    }

    public BlockProductSource? ParseProductSource(string? configJson) =>
        ParseNode<BlockProductSource>(configJson, "productSource");

    public BlockCollectionSource? ParseCollectionSource(string? configJson) =>
        ParseNode<BlockCollectionSource>(configJson, "collectionSource");

    private static T? ParseNode<T>(string? configJson, string node) where T : class
    {
        if (string.IsNullOrWhiteSpace(configJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            return doc.RootElement.TryGetProperty(node, out var el)
                ? el.Deserialize<T>(JsonOpts)
                : null;
        }
        catch (JsonException)
        {
            return null; // bozuk config yayını düşürmez — blok kaynaksız kalır
        }
    }

    private async Task<List<StoreProductDto>> KartlariGetirAsync(
        Guid firmPlatformId, BlockProductSource source, int page, int limit,
        List<string>? productCodes = null, List<Guid>? productIds = null,
        List<Guid>? attributeValueIds = null, CancellationToken ct = default)
    {
        var sonuc = await mediator.Send(new GetStoreProductsQuery(
            firmPlatformId, null, page, limit,
            attributeValueIds ?? source.AttributeValueIds,
            source.PriceMin, source.PriceMax, source.Sort,
            productCodes, productIds), ct);
        return sonuc.IsSuccess ? sonuc.Value!.Items.ToList() : [];
    }

    /// <summary>Varyant id sırasını koruyarak tekil ürün kodu listesi (IProductService, E7 deseni).</summary>
    private async Task<List<string>> VaryantlariKodaCevirAsync(List<Guid> variantIds, CancellationToken ct)
    {
        if (variantIds.Count == 0) return [];
        var bilgiler = await productService.GetVariantDisplayAsync(variantIds, ct);
        var kodlar = new List<string>();
        foreach (var id in variantIds)
            if (bilgiler.TryGetValue(id, out var b) && !kodlar.Contains(b.ProductCode))
                kodlar.Add(b.ProductCode);
        return kodlar;
    }

    /// <summary>Kanal kategori kart DTO'sunu ortak kart sözleşmesine indirger.</summary>
    private static StoreProductDto KanalKartinaCevir(ChannelCategoryProductItemDto p) => new(
        p.ProductId, p.Code, p.NameI18n, null,
        p.MainImageUrl, p.BasePrice, null, p.IsActive,
        p.Colors ?? [], p.Attrs ?? [], p.GalleryUrls,
        p.IsFeatured, p.Rating, p.ReviewCount);
}
