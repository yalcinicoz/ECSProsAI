using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using MediatR;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// B1 (2026-09-07, mobil): "yalnız kod dönen" üye listeleri (favoriler, gezilenler, yorumlarım, sorularım,
/// koleksiyonlar) için ürün kodu → kart verisi (ad, görsel, fiyat, çizili fiyat, kampanya) toplu çözümü —
/// istemci her satır için ayrı ürün detayı çekmesin (N+1). Kaynak: GetStoreProductsQuery(ProductCodes)
/// — liste kartıyla AYNI fiyat/görsel kuralı. Satışa kapalı/silinmiş ürün sözlükte yer almaz.
/// </summary>
public class StoreKartZenginlestirici(IMediator mediator)
{
    private const int ParcaBoyu = 100;

    public async Task<Dictionary<string, StoreProductDto>> GetirAsync(
        Guid firmPlatformId, IEnumerable<string> productCodes, CancellationToken ct)
    {
        var sonuc = new Dictionary<string, StoreProductDto>(StringComparer.OrdinalIgnoreCase);
        var kodlar = productCodes.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var parca in kodlar.Chunk(ParcaBoyu))
        {
            try
            {
                var r = await mediator.Send(new GetStoreProductsQuery(
                    firmPlatformId, Page: 1, PageSize: parca.Length, ProductCodes: parca.ToList(),
                    ApplyStockFilter: false), ct);
                if (r.IsFailure) continue;
                foreach (var p in r.Value!.Items) sonuc.TryAdd(p.Code, p);
            }
            catch { /* zenginleştirme isteğe bağlı — liste kodlarla döner */ }
        }
        return sonuc;
    }

    public static string Ad(StoreProductDto p) =>
        p.NameI18n.TryGetValue("tr", out var tr) ? tr : p.NameI18n.Values.FirstOrDefault() ?? p.Code;

    /// <summary>Renge göre görsel: colorValueId verilmiş ve o rengin görseli varsa o; yoksa kartın ana görseli.</summary>
    public static string? Gorsel(StoreProductDto p, Guid? colorValueId = null)
    {
        if (colorValueId is { } cid)
        {
            var renk = p.Colors.FirstOrDefault(c => c.ValueId == cid);
            if (renk?.ImageUrl is { } url) return url;
        }
        return p.MainImageUrl;
    }

    public static StoreUrunOzetDto? Ozet(Dictionary<string, StoreProductDto> harita, string code, Guid? colorValueId = null) =>
        harita.TryGetValue(code, out var p)
            ? new StoreUrunOzetDto(p.Code, Ad(p), Gorsel(p, colorValueId), p.MinPrice, p.CompareAtPrice, p.CampaignPrice, p.IsActive)
            : null;
}

/// <summary>B1: liste satırına gömülü ürün özeti (kartla aynı fiyat kuralı).</summary>
public record StoreUrunOzetDto(
    string ProductCode,
    string ProductName,
    string? ImageUrl,
    decimal MinPrice,
    decimal? CompareAtPrice,
    decimal? CampaignPrice,
    bool IsActive);
