using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Infrastructure.Services;

/// <summary>
/// IProductService implementasyonu — diğer modüllerin (CRM sepeti, Order satırları)
/// varyant bilgisine Catalog'a doğrudan referans vermeden erişmesi için (B5).
/// </summary>
public class CatalogProductService(ICatalogDbContext db) : IProductService
{
    public async Task<ProductInfo?> GetVariantAsync(Guid variantId, CancellationToken ct = default)
    {
        return await db.ProductVariants.AsNoTracking()
            .Where(v => v.Id == variantId)
            .Select(v => new ProductInfo(
                v.Id, v.Sku,
                v.Product.NameI18n.ContainsKey("tr") ? v.Product.NameI18n["tr"] : v.Product.Code,
                // Satılabilir = varyant aktif VE ürün global satışa açık (Katman 1).
                v.BasePrice, v.IsActive && v.Product.IsSaleOpen, v.ProductId,
                v.Product.SupplierId))
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> VariantExistsAsync(Guid variantId, CancellationToken ct = default) =>
        db.ProductVariants.AsNoTracking().AnyAsync(v => v.Id == variantId, ct);

    public async Task<Dictionary<Guid, VariantDisplayInfo>> GetVariantDisplayAsync(
        IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default)
    {
        if (variantIds.Count == 0)
            return new Dictionary<Guid, VariantDisplayInfo>();

        var ids = variantIds.Distinct().ToList();
        var cdnBase = await CdnHelper.BuildListUrlAsync(db, ct);

        var varyantlar = await db.ProductVariants.AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .Select(v => new { v.Id, v.ProductId, ProductCode = v.Product.Code, v.Product.NameI18n })
            .ToListAsync(ct);

        var productIds = varyantlar.Select(v => v.ProductId).Distinct().ToList();

        // Görsel çözümü (2026-07-30): görseller renk başına TEMSİLCİ varyanta bağlanır; sepetteki
        // beden varyantının kendi görseli olmayabilir. Önce varyantın KENDİ görseli → yoksa varyantın
        // RENGİNE ait görsel (aynı ürün + aynı 'renk' değeri, görselli kardeş varyant) → yoksa ürünün
        // ilk görseli. (Eskiden ikinci adım yoktu → Siyah varyant ürünün ilk (Krem) görseline düşüyordu.)
        var tumVaryantGorselleri = await db.ProductImages.AsNoTracking()
            .Where(img => img.VariantId != null && productIds.Contains(img.ProductId)
                       && img.Status == ProductImageStatus.Active)
            .Select(img => new { VariantId = img.VariantId!.Value, img.ProductId, img.FileName, img.SortOrder })
            .ToListAsync(ct);

        // İlgili ürünlerin tüm varyantlarının 'renk' değeri (hem sepet varyantı hem görselli temsilci için).
        var renkByVariant = (await db.ProductVariantAttributes.AsNoTracking()
            .Where(va => productIds.Contains(va.Variant.ProductId) && va.AttributeType.Code == "renk")
            .Select(va => new { va.VariantId, va.AttributeValueId })
            .ToListAsync(ct))
            .GroupBy(x => x.VariantId)
            .ToDictionary(g => g.Key, g => g.First().AttributeValueId);

        // Varyantın kendi görseli (sortOrder'a göre kapak)
        var varyantGorselleri = tumVaryantGorselleri
            .GroupBy(g => g.VariantId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SortOrder).First().FileName);

        // (ProductId, renkDeğeri) → o rengin ilk görseli (görselli kardeş varyanttan)
        var renkGorselleri = tumVaryantGorselleri
            .Where(g => renkByVariant.ContainsKey(g.VariantId))
            .GroupBy(g => (g.ProductId, RenkId: renkByVariant[g.VariantId]))
            .ToDictionary(grp => grp.Key, grp => grp.OrderBy(x => x.SortOrder).First().FileName);

        var urunGorselleri = await db.ProductImages.AsNoTracking()
            .Where(img => productIds.Contains(img.ProductId) && img.Status == ProductImageStatus.Active)
            .GroupBy(img => img.ProductId)
            .Select(g => new { g.Key, Fn = g.OrderBy(i => i.SortOrder).First().FileName })
            .ToDictionaryAsync(x => x.Key, x => x.Fn, ct);

        string? GorselCoz(Guid variantId, Guid productId)
        {
            if (varyantGorselleri.TryGetValue(variantId, out var vg)) return cdnBase + vg;
            if (renkByVariant.TryGetValue(variantId, out var renkId)
                && renkGorselleri.TryGetValue((productId, renkId), out var rg)) return cdnBase + rg;
            if (urunGorselleri.TryGetValue(productId, out var ug)) return cdnBase + ug;
            return null;
        }

        // Seçenek özeti: varyant eksen değerleri ("Beden: M, Renk: Beyaz") —
        // filtre_rengi iç facet tipidir, müşteri özetine girmez.
        var attrSatirlari = await db.ProductVariantAttributes.AsNoTracking()
            .Where(va => ids.Contains(va.VariantId) && va.AttributeType.Code != "filtre_rengi")
            .Select(va => new
            {
                va.VariantId,
                TipKodu = va.AttributeType.Code,
                TipAd = va.AttributeType.NameI18n,
                DegerAd = va.AttributeValue.NameI18n
            })
            .ToListAsync(ct);

        static string TrAd(Dictionary<string, string> i18n, string yedek = "") =>
            i18n.TryGetValue("tr", out var ad) ? ad : i18n.Values.FirstOrDefault() ?? yedek;

        var ozetByVariant = attrSatirlari
            .GroupBy(a => a.VariantId)
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ", g
                    .GroupBy(a => a.TipKodu).Select(t => t.First())
                    .OrderBy(a => a.TipKodu, StringComparer.Ordinal)
                    .Take(3)
                    .Select(a => $"{TrAd(a.TipAd, a.TipKodu)}: {TrAd(a.DegerAd)}")));

        return varyantlar.ToDictionary(
            v => v.Id,
            v => new VariantDisplayInfo(
                v.Id,
                v.ProductCode,
                v.NameI18n,
                GorselCoz(v.Id, v.ProductId),
                ozetByVariant.TryGetValue(v.Id, out var ozet) && ozet.Length > 0 ? ozet : null));
    }
}
