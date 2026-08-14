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

        // Tüm renk kartları (2026-08-15, kullanıcı isteği): her model RENK başına ayrı kartla
        // döner (kategori renk-kartı kuralı gibi; görselsiz renk listelenmez, renk ekseni
        // olmayan ürün tek kart). MatchedBarcodes verilmişse eşleşen renk İLK sırada.
        var tumVaryantIdler = products.SelectMany(p => p.Variants).Select(v => v.Id).ToList();
        var renkSatirlari = tumVaryantIdler.Count > 0
            ? await db.ProductVariantAttributes.AsNoTracking()
                .Where(va => tumVaryantIdler.Contains(va.VariantId)
                          && (va.AttributeType.Code == "filtre_rengi" || va.AttributeType.Code == "renk"))
                .Select(va => new { va.VariantId, TipKodu = va.AttributeType.Code, va.AttributeValueId })
                .ToListAsync(ct)
            : [];
        // varyant → renk değeri (filtre_rengi öncelikli, yoksa renk ekseni — detay ?color= ikisini de çözer)
        var renkByVariant = renkSatirlari
            .GroupBy(x => x.VariantId)
            .ToDictionary(g => g.Key,
                g => (g.FirstOrDefault(x => x.TipKodu == "filtre_rengi") ?? g.First()).AttributeValueId);
        var varyantIlkGorseller = tumVaryantIdler.Count > 0
            ? await db.ProductImages.AsNoTracking()
                .Where(i => i.VariantId != null && tumVaryantIdler.Contains(i.VariantId.Value)
                         && i.Status == Domain.Entities.ProductImageStatus.Active)
                .GroupBy(i => i.VariantId!.Value)
                .Select(g => new { VariantId = g.Key, Fn = g.OrderBy(i => i.SortOrder).First().FileName })
                .ToDictionaryAsync(x => x.VariantId, x => x.Fn, ct)
            : new Dictionary<Guid, string>();

        // Eşleşen renk: barkodla verilen varyantın rengi — model içinde ilk sıraya alınır
        var eslesenRenkByProduct = new Dictionary<Guid, Guid>();
        if (request.MatchedBarcodes is { Count: > 0 } barkodlar)
        {
            foreach (var p in products)
            {
                if (barkodlar.TryGetValue(p.Code, out var barkod) && barkod is { Length: > 0 })
                {
                    var varyant = p.Variants.FirstOrDefault(v => v.Barcode == barkod);
                    if (varyant is not null && renkByVariant.TryGetValue(varyant.Id, out var renkId))
                        eslesenRenkByProduct[p.Id] = renkId;
                }
            }
        }

        var cards = products.SelectMany(p =>
        {
            var activeVariants = p.Variants.ToList();
            var platformPrices = activeVariants
                .Where(v => channelPrices.ContainsKey(v.Id))
                .Select(v => channelPrices[v.Id].Price ?? 0)
                .Where(price => price > 0)
                .ToList();
            var variantMin = activeVariants.Count > 0 ? activeVariants.Min(v => v.BasePrice) : 0;
            var price = platformPrices.Count > 0 ? platformPrices.Min() : variantMin > 0 ? variantMin : p.BasePrice;

            // Renk kartları: rengin ilk görselli varyantından görsel; görselsiz renk atlanır
            var renkKartlari = new List<(Guid RenkId, string Gorsel)>();
            foreach (var v in p.Variants)
            {
                if (!renkByVariant.TryGetValue(v.Id, out var renkId)) continue;
                if (renkKartlari.Any(r => r.RenkId == renkId)) continue;
                var renkGorseli = p.Variants
                    .Where(v2 => renkByVariant.TryGetValue(v2.Id, out var r2) && r2 == renkId)
                    .Select(v2 => varyantIlkGorseller.GetValueOrDefault(v2.Id))
                    .FirstOrDefault(f => f is not null);
                if (renkGorseli is null) continue;
                renkKartlari.Add((renkId, cdnBase + renkGorseli));
            }

            // Slug çözümü kanal-özel (Storefront); güvenlik ağı /urun/{code} in-place render eder.
            if (renkKartlari.Count == 0)
            {
                // Renk ekseni/renk görseli yok: bugünkü tek kart davranışı
                var image = firstImages.TryGetValue(p.Id, out var fn) ? cdnBase + fn : null;
                return new List<VisualSearchCardDto>
                    { new(p.Code, p.NameI18n, image, price, "/urun/" + p.Code) };
            }

            // Eşleşen renk (aranan görseldeki renk) ilk sırada
            if (eslesenRenkByProduct.TryGetValue(p.Id, out var eslesenRenk))
                renkKartlari = renkKartlari
                    .OrderByDescending(r => r.RenkId == eslesenRenk)
                    .ToList();

            return renkKartlari.Select(r => new VisualSearchCardDto(
                p.Code, p.NameI18n, r.Gorsel, price, "/urun/" + p.Code + "?color=" + r.RenkId)).ToList();
        }).ToList();

        return Result.Success(cards);
    }
}
