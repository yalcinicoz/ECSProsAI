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
public record GetVisualSearchCardsQuery(Guid FirmPlatformId, List<string> ModelCodes)
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
            return new VisualSearchCardDto(p.Code, p.NameI18n, image, price, "/urun/" + p.Code);
        }).ToList();

        return Result.Success(cards);
    }
}
