using ECSPros.Catalog.Application.Services;
using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Storefront.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// Sosyal kanıt sayaçları (2026-08-10): sepet CRM'de, favori Storefront'ta,
/// varyant→ürün eşlemesi Catalog'da olduğundan implementasyon host'ta. Sayaçlar
/// ürün başına IMemoryCache'te tutulur (5 dk) — kart query'leri her istekte çağırsa da
/// DB'ye ancak TTL dolunca gidilir; sıfır sayaçlar da cache'lenir (tekrar hesaplanmaz).
/// </summary>
public sealed class SosyalKanitResolver(
    ICatalogDbContext catDb,
    ICrmDbContext crmDb,
    IStorefrontDbContext sfDb,
    IMemoryCache cache) : ISocialProofResolver
{
    private static readonly TimeSpan CacheSuresi = TimeSpan.FromMinutes(5);
    /// <summary>Terk edilmiş eski sepetler sayacı şişirmesin — yalnız son 30 günde eklenen kalemler.</summary>
    private static readonly TimeSpan SepetPenceresi = TimeSpan.FromDays(30);

    public async Task<Dictionary<Guid, SocialProofCounts>> ResolveForProductsAsync(
        Guid firmPlatformId,
        IReadOnlyDictionary<Guid, string> productCodesById,
        CancellationToken ct = default)
    {
        var sonuc = new Dictionary<Guid, SocialProofCounts>();
        if (productCodesById.Count == 0) return sonuc;

        var eksikler = new List<Guid>();
        foreach (var pid in productCodesById.Keys)
        {
            if (cache.TryGetValue<SocialProofCounts>(Anahtar(firmPlatformId, pid), out var c) && c is not null)
            {
                if (c.CartCount > 0 || c.FavoriteCount > 0 || c.ViewCount > 0) sonuc[pid] = c;
            }
            else
            {
                eksikler.Add(pid);
            }
        }
        if (eksikler.Count == 0) return sonuc;

        // Favoriler: ürün kodu bazlı, farklı üye sayısı (renk bazlı favoriler tek üyeye iner)
        var kodByPid = eksikler.ToDictionary(pid => pid, pid => productCodesById[pid]);
        var kodlar = kodByPid.Values.Distinct().ToList();
        var favoriByKod = await sfDb.Favorites.AsNoTracking()
            .Where(f => f.FirmPlatformId == firmPlatformId && kodlar.Contains(f.ProductCode))
            .GroupBy(f => f.ProductCode)
            .Select(g => new { Kod = g.Key, Sayi = g.Select(x => x.MemberId).Distinct().Count() })
            .ToDictionaryAsync(g => g.Kod, g => g.Sayi, ct);

        // Görüntülenme: ürünü gezen farklı üye (viewed_products üye başına tek satır → satır sayısı)
        var bakanByKod = await sfDb.ViewedProducts.AsNoTracking()
            .Where(v => v.FirmPlatformId == firmPlatformId && kodlar.Contains(v.ProductCode))
            .GroupBy(v => v.ProductCode)
            .Select(g => new { Kod = g.Key, Sayi = g.Count() })
            .ToDictionaryAsync(g => g.Kod, g => g.Sayi, ct);

        // Sepetler: ürünün varyantlarını içeren farklı sepet sayısı (son 30 gün)
        var varyantlar = await catDb.ProductVariants.AsNoTracking()
            .Where(v => eksikler.Contains(v.ProductId))
            .Select(v => new { v.Id, v.ProductId })
            .ToListAsync(ct);
        var pidByVaryant = varyantlar.ToDictionary(v => v.Id, v => v.ProductId);
        var sepetByPid = new Dictionary<Guid, HashSet<Guid>>();
        if (pidByVaryant.Count > 0)
        {
            var varyantIdler = pidByVaryant.Keys.ToList();
            var kesim = DateTime.UtcNow - SepetPenceresi;
            var ciftler = await crmDb.CartItems.AsNoTracking()
                .Where(ci => varyantIdler.Contains(ci.VariantId)
                             && ci.AddedAt >= kesim
                             && ci.Cart.FirmPlatformId == firmPlatformId)
                .Select(ci => new { ci.VariantId, ci.CartId })
                .Distinct()
                .ToListAsync(ct);
            foreach (var c in ciftler)
            {
                var pid = pidByVaryant[c.VariantId];
                if (!sepetByPid.TryGetValue(pid, out var set))
                    sepetByPid[pid] = set = new HashSet<Guid>();
                set.Add(c.CartId);
            }
        }

        foreach (var pid in eksikler)
        {
            var sayilar = new SocialProofCounts(
                CartCount: sepetByPid.TryGetValue(pid, out var sepetler) ? sepetler.Count : 0,
                FavoriteCount: favoriByKod.GetValueOrDefault(kodByPid[pid]),
                ViewCount: bakanByKod.GetValueOrDefault(kodByPid[pid]));
            cache.Set(Anahtar(firmPlatformId, pid), sayilar, CacheSuresi);
            if (sayilar.CartCount > 0 || sayilar.FavoriteCount > 0 || sayilar.ViewCount > 0) sonuc[pid] = sayilar;
        }
        return sonuc;
    }

    private static string Anahtar(Guid firmPlatformId, Guid productId) =>
        $"sosyal-kanit:{firmPlatformId}:{productId}";
}
