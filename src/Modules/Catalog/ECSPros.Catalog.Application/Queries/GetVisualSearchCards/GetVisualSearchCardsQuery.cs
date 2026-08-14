using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetVisualSearchCards;

/// <summary>
/// H3 görsel arama sonuç kartları: model kodlarına (products.Code) göre storefront kart bilgisi.
/// Fiyat önceliği liste sayfasıyla AYNI (GetStoreProducts): kanal fiyatı → aktif varyant min
/// BasePrice → ürün BasePrice. Yalnız satışa açık ürünler döner (kapalı ürün kartı kırık link olur).
/// </summary>
/// <param name="MatchedBarcodes">Eşleşen renk (2026-08-15): modelCode → dış servisin bulduğu
/// varyantın BARKODU. Verilirse kart o varyantın rengi (?color=) ve görseliyle döner —
/// siyah görselle arandıysa kart siyah görünür. Barkod katalogda bulunamazsa varsayılana düşer.</param>
public record GetVisualSearchCardsQuery(
    Guid FirmPlatformId,
    List<string> ModelCodes,
    Dictionary<string, string>? MatchedBarcodes = null)
    : IRequest<Result<List<VisualSearchCardDto>>>;

public record VisualSearchCardDto(
    string ModelCode,
    Dictionary<string, string> Name,
    string? ImageUrl,
    decimal Price,
    string Url);

public class GetVisualSearchCardsQueryHandler(
    ICatalogDbContext db,
    IChannelPricingService pricingService)
    : IRequestHandler<GetVisualSearchCardsQuery, Result<List<VisualSearchCardDto>>>
{
    public async Task<Result<List<VisualSearchCardDto>>> Handle(GetVisualSearchCardsQuery request, CancellationToken ct)
    {
        if (request.ModelCodes.Count == 0)
            return Result.Success(new List<VisualSearchCardDto>());

        var codes = request.ModelCodes.Distinct().ToList();

        var products = await db.Products
            .AsNoTracking()
            .Include(p => p.Variants.Where(v => !v.IsDeleted && v.IsActive))
            .Where(p => codes.Contains(p.Code) && p.IsSaleOpen)
            .ToListAsync(ct);

        if (products.Count == 0)
            return Result.Success(new List<VisualSearchCardDto>());

        var productIds = products.Select(p => p.Id).ToList();

        var cdnBase = await CdnHelper.BuildListUrlAsync(db, ct);
        var channelPrices = await pricingService.GetActiveVariantPricesAsync(request.FirmPlatformId, ct);

        // Ürün başına ilk görsel (liste sayfası kalıbı)
        var firstImages = await db.ProductImages
            .AsNoTracking()
            .Where(img => productIds.Contains(img.ProductId))
            .GroupBy(img => img.ProductId)
            .Select(g => new { ProductId = g.Key, FileName = g.OrderBy(i => i.SortOrder).First().FileName })
            .ToDictionaryAsync(x => x.ProductId, x => x.FileName, ct);

        // Eşleşen renk: barkodla verilen varyantın ilk görseli + renk değeri (filtre_rengi
        // öncelikli, yoksa renk ekseni — detay ?color= ikisini de çözer)
        var eslesenVaryantlar = new Dictionary<Guid, Guid>(); // ProductId → VariantId
        if (request.MatchedBarcodes is { Count: > 0 } barkodlar)
        {
            foreach (var p in products)
            {
                if (barkodlar.TryGetValue(p.Code, out var barkod) && barkod is { Length: > 0 })
                {
                    var varyant = p.Variants.FirstOrDefault(v => v.Barcode == barkod);
                    if (varyant is not null) eslesenVaryantlar[p.Id] = varyant.Id;
                }
            }
        }
        var eslesenVaryantIdler = eslesenVaryantlar.Values.ToList();
        var eslesenGorseller = eslesenVaryantIdler.Count > 0
            ? await db.ProductImages.AsNoTracking()
                .Where(i => i.VariantId != null && eslesenVaryantIdler.Contains(i.VariantId.Value)
                         && i.Status == Domain.Entities.ProductImageStatus.Active)
                .GroupBy(i => i.VariantId!.Value)
                .Select(g => new { VariantId = g.Key, Fn = g.OrderBy(i => i.SortOrder).First().FileName })
                .ToDictionaryAsync(x => x.VariantId, x => x.Fn, ct)
            : new Dictionary<Guid, string>();
        var eslesenRenkler = eslesenVaryantIdler.Count > 0
            ? (await db.ProductVariantAttributes.AsNoTracking()
                .Where(va => eslesenVaryantIdler.Contains(va.VariantId)
                          && (va.AttributeType.Code == "filtre_rengi" || va.AttributeType.Code == "renk"))
                .Select(va => new { va.VariantId, TipKodu = va.AttributeType.Code, va.AttributeValueId })
                .ToListAsync(ct))
                .GroupBy(x => x.VariantId)
                .ToDictionary(g => g.Key,
                    g => (g.FirstOrDefault(x => x.TipKodu == "filtre_rengi") ?? g.First()).AttributeValueId)
            : new Dictionary<Guid, Guid>();

        var cards = products.Select(p =>
        {
            var activeVariants = p.Variants.ToList();
            var platformPrices = activeVariants
                .Where(v => channelPrices.ContainsKey(v.Id))
                .Select(v => channelPrices[v.Id].Price ?? 0)
                .Where(price => price > 0)
                .ToList();
            var variantMin = activeVariants.Count > 0 ? activeVariants.Min(v => v.BasePrice) : 0;
            var price = platformPrices.Count > 0 ? platformPrices.Min() : variantMin > 0 ? variantMin : p.BasePrice;

            var image = firstImages.TryGetValue(p.Id, out var fn) ? cdnBase + fn : null;

            // Slug çözümü kanal-özel (Storefront); güvenlik ağı /urun/{code} in-place render eder.
            var url = "/urun/" + p.Code;
            if (eslesenVaryantlar.TryGetValue(p.Id, out var eslesenVaryantId))
            {
                if (eslesenGorseller.TryGetValue(eslesenVaryantId, out var eslesenFn))
                    image = cdnBase + eslesenFn;
                if (eslesenRenkler.TryGetValue(eslesenVaryantId, out var renkId))
                    url += "?color=" + renkId;
            }
            return new VisualSearchCardDto(p.Code, p.NameI18n, image, price, url);
        }).ToList();

        return Result.Success(cards);
    }
}
