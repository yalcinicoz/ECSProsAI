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
                v.BasePrice, v.IsActive && v.Product.IsSaleOpen, v.ProductId))
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

        // Görsel: önce varyantın (rengin) kendi görseli, yoksa ürünün ilk görseli
        var varyantGorselleri = await db.ProductImages.AsNoTracking()
            .Where(img => img.VariantId != null && ids.Contains(img.VariantId.Value)
                       && img.Status == ProductImageStatus.Active)
            .GroupBy(img => img.VariantId!.Value)
            .Select(g => new { VariantId = g.Key, Fn = g.OrderBy(i => i.SortOrder).First().FileName })
            .ToDictionaryAsync(x => x.VariantId, x => x.Fn, ct);

        var urunGorselleri = await db.ProductImages.AsNoTracking()
            .Where(img => productIds.Contains(img.ProductId))
            .GroupBy(img => img.ProductId)
            .Select(g => new { g.Key, Fn = g.OrderBy(i => i.SortOrder).First().FileName })
            .ToDictionaryAsync(x => x.Key, x => x.Fn, ct);

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
                varyantGorselleri.TryGetValue(v.Id, out var vg) ? cdnBase + vg
                    : urunGorselleri.TryGetValue(v.ProductId, out var ug) ? cdnBase + ug : null,
                ozetByVariant.TryGetValue(v.Id, out var ozet) && ozet.Length > 0 ? ozet : null));
    }
}
