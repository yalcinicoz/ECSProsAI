using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Queries.GetStoreProductDetail;
using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Handlers;

public class GetStoreProductDetailHandler(ICatalogDbContext db, IInventoryDbContext invDb, IChannelPricingService pricingService)
    : IRequestHandler<GetStoreProductDetailQuery, Result<StoreProductDetailDto>>
{
    public async Task<Result<StoreProductDetailDto>> Handle(GetStoreProductDetailQuery request, CancellationToken ct)
    {
        var cdnBase = await CdnHelper.BuildListUrlAsync(db, ct);
        var channelPrices = await pricingService.GetActiveVariantPricesAsync(request.FirmPlatformId, ct);

        var product = await db.Products
            .AsNoTracking()
            .Include(p => p.Variants).ThenInclude(v => v.VariantAttributes).ThenInclude(va => va.AttributeType)
            .Include(p => p.Variants).ThenInclude(v => v.VariantAttributes).ThenInclude(va => va.AttributeValue)
            .FirstOrDefaultAsync(p => p.Code == request.ProductCode && p.IsActive, ct);

        if (product is null)
            return Result.Failure<StoreProductDetailDto>("Ürün bulunamadı.");

        var activeVariantIds = product.Variants.Where(v => v.IsActive).Select(v => v.Id).ToList();

        // Hex kodları — AttributeValue.HexCode direkt kullanılır
        var hexByValueId = product.Variants
            .SelectMany(v => v.VariantAttributes)
            .Where(va => va.AttributeType.Code == "filtre_rengi" && va.AttributeValue.HexCode != null)
            .GroupBy(va => va.AttributeValue.Id)
            .ToDictionary(g => g.Key, g => g.First().AttributeValue.HexCode!);

        // Görseller — FileName+VariantId bazlı deduplicate (DB'de aynı resim çift kayıtlı olabilir)
        var allImgs = (await db.ProductImages.AsNoTracking()
            .Where(img => img.ProductId == product.Id && img.Status == ProductImageStatus.Active)
            .OrderBy(img => img.SortOrder)
            .Select(img => new { img.Id, img.FileName, img.SortOrder, img.IsProductCover, img.VariantId })
            .ToListAsync(ct))
            .GroupBy(i => (i.FileName, i.VariantId))
            .Select(g => g.First())
            .ToList();

        var rawByVariantId = allImgs
            .Where(i => i.VariantId.HasValue)
            .GroupBy(i => i.VariantId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Renk grubu: filtre_rengi varsa o, yoksa renk ekseni. Migrasyon verisinde filtre_rengi
        // hiç atanmamış durumda; renk'e bakılmazsa görselsiz varyantlar tüm renklerin karışık
        // ürün-düzeyi havuzuna düşüyordu (aynı poz her renkten bir kez → "tekrarlı" galeri).
        var variantColorValue = product.Variants.Where(v => v.IsActive)
            .Select(v => new
            {
                VariantId = v.Id,
                ColorValueId = v.VariantAttributes
                    .Where(va => va.AttributeType.Code == "filtre_rengi")
                    .Select(va => (Guid?)va.AttributeValue.Id).FirstOrDefault()
                    ?? v.VariantAttributes
                    .Where(va => va.AttributeType.Code == "renk")
                    .Select(va => (Guid?)va.AttributeValue.Id).FirstOrDefault()
            })
            .Where(x => x.ColorValueId.HasValue)
            .ToDictionary(x => x.VariantId, x => x.ColorValueId!.Value);

        var colorToVariantIds = variantColorValue
            .GroupBy(kv => kv.Value, kv => kv.Key)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Aynı renkteki varyantların görselleri birleşirken dosya adına göre teke iner:
        // aynı fotoğraf birden çok varyanta ayrı kayıtla bağlı olabilir.
        var imgsByColor = colorToVariantIds.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value
                .SelectMany(vid => rawByVariantId.TryGetValue(vid, out var imgs) ? imgs : [])
                .GroupBy(i => i.FileName).Select(g => g.First())
                .OrderBy(i => i.SortOrder)
                .Select(i => new StoreVariantImageDto(i.Id, cdnBase + i.FileName, i.SortOrder, i.IsProductCover))
                .ToList());

        var imgsByVariantId = rawByVariantId.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value
                .Select(i => new StoreVariantImageDto(i.Id, cdnBase + i.FileName, i.SortOrder, i.IsProductCover))
                .ToList());

        var productImages = allImgs
            .Where(i => !i.VariantId.HasValue)
            .GroupBy(i => i.FileName).Select(g => g.First())
            .OrderBy(i => i.SortOrder)
            .Select(i => new StoreVariantImageDto(i.Id, cdnBase + i.FileName, i.SortOrder, i.IsProductCover))
            .ToList();

        // Stok
        var stockByVariant = activeVariantIds.Count > 0
            ? await invDb.Stocks.AsNoTracking()
                .Where(s => activeVariantIds.Contains(s.VariantId))
                .GroupBy(s => s.VariantId)
                .Select(g => new { VariantId = g.Key, Total = g.Sum(s => s.Quantity - s.ReservedQuantity) })
                .ToDictionaryAsync(x => x.VariantId, x => x.Total, ct)
            : new Dictionary<Guid, int>();

        var variants = product.Variants.Where(v => v.IsActive).Select(v =>
        {
            channelPrices.TryGetValue(v.Id, out var channelPrice);
            var attrs = v.VariantAttributes.Select(a => new StoreVariantAttributeDto(
                a.AttributeType.Code, a.AttributeType.NameI18n,
                a.AttributeValue.Id, a.AttributeValue.NameI18n,
                IsColor: a.AttributeType.Code == "filtre_rengi",
                HexCode: hexByValueId.GetValueOrDefault(a.AttributeValue.Id))).ToList();

            // Öncelik: renk grubu görselleri (filtre_rengi/renk) → varyantın kendi görselleri
            // (VariantId doğrudan eşleşen) → ürün düzeyinde ortak görseller (VariantId=null).
            // Renk ekseni hiç olmayan kataloglarda ikinci adım olmasa varyanta özel görseller
            // sessizce kaybolurdu. Ürün düzeyi havuz yalnızca üründe hiç varyant-bağlı görsel
            // yoksa devreye girer: migrasyonda bu havuz eşleşemeyen eski varyantların TÜM
            // renklerinin görsellerini içeriyor — görselsiz bir renge verilirse galeri her pozu
            // renk sayısı kadar tekrar gösteriyor. Boş dönmek daha iyi; UI görseli olan ilk
            // varyanta düşüyor.
            List<StoreVariantImageDto> variantImages;
            if (variantColorValue.TryGetValue(v.Id, out var colorId)
                && imgsByColor.TryGetValue(colorId, out var cImgs) && cImgs.Count > 0)
                variantImages = cImgs;
            else if (imgsByVariantId.TryGetValue(v.Id, out var ownImgs) && ownImgs.Count > 0)
                variantImages = ownImgs;
            else if (rawByVariantId.Count == 0)
                variantImages = productImages;
            else
                variantImages = [];

            return new StoreVariantDto(
                v.Id, v.Sku, v.BasePrice, channelPrice?.Price, channelPrice?.CompareAtPrice,
                v.IsActive, variantImages, attrs,
                stockByVariant.GetValueOrDefault(v.Id, 0));
        }).ToList();

        return Result.Success(new StoreProductDetailDto(
            product.Id, product.Code, product.NameI18n, product.ShortDescriptionI18n,
            product.IsActive, variants));
    }
}
