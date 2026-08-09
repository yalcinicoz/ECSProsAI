using ECSPros.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Services;

/// <summary>
/// Ürün Kartı F2: kanalın aktif kart mesajlarını sayfadaki ürünlere dağıtır.
/// Tek mesaj sorgusu + (kategori kapsamı varsa) tek üyelik sorgusu — kart query'lerinde
/// cache SONRASI çağrılır (kampanya resolver kalıbı), mesaj değişikliği TTL beklemez.
/// </summary>
public class CardMessageResolver(IStorefrontDbContext db) : ICardMessageResolver
{
    public async Task<Dictionary<Guid, List<CardMessageItem>>> ResolveForProductsAsync(
        Guid firmPlatformId,
        IReadOnlyDictionary<Guid, string> productCodesById,
        CancellationToken ct = default)
    {
        var sonuc = new Dictionary<Guid, List<CardMessageItem>>();
        if (productCodesById.Count == 0) return sonuc;

        var now = DateTime.UtcNow;
        var mesajlar = (await db.CardMessages.AsNoTracking()
                .Where(m => m.FirmPlatformId == firmPlatformId && m.IsActive)
                .OrderBy(m => m.Slot).ThenBy(m => m.SortOrder).ThenBy(m => m.CreatedAt)
                .ToListAsync(ct))
            .Where(m => m.IsLiveAt(now))
            .ToList();
        if (mesajlar.Count == 0) return sonuc;

        // Kategori kapsamlı mesajların üyelik eşlemesi: ProductId → kapsandığı kategori seti
        var kapsamKategorileri = mesajlar
            .Where(m => m.ScopeType == "category" && m.ScopeCategoryIds is { Count: > 0 })
            .SelectMany(m => m.ScopeCategoryIds!)
            .Distinct()
            .ToList();
        var uyelik = new Dictionary<Guid, HashSet<Guid>>();
        if (kapsamKategorileri.Count > 0)
        {
            var pidler = productCodesById.Keys.ToList();
            var satirlar = await db.ChannelCategoryProducts.AsNoTracking()
                .Where(cp => kapsamKategorileri.Contains(cp.ChannelCategoryId)
                             && pidler.Contains(cp.ProductId) && !cp.IsExcluded)
                .Select(cp => new { cp.ProductId, cp.ChannelCategoryId })
                .ToListAsync(ct);
            foreach (var s in satirlar)
            {
                if (!uyelik.TryGetValue(s.ProductId, out var set))
                    uyelik[s.ProductId] = set = new HashSet<Guid>();
                set.Add(s.ChannelCategoryId);
            }
        }

        foreach (var (pid, kod) in productCodesById)
        {
            foreach (var m in mesajlar)
            {
                var kapsar = m.ScopeType switch
                {
                    "category" => m.ScopeCategoryIds is { Count: > 0 }
                                  && uyelik.TryGetValue(pid, out var set)
                                  && m.ScopeCategoryIds.Any(set.Contains),
                    "products" => m.ScopeProductCodes is { Count: > 0 }
                                  && m.ScopeProductCodes.Contains(kod, StringComparer.OrdinalIgnoreCase),
                    _ => true // all
                };
                if (!kapsar) continue;

                var metin = m.MessageI18n.TryGetValue("tr", out var tr) && tr is { Length: > 0 }
                    ? tr
                    : m.MessageI18n.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                if (string.IsNullOrWhiteSpace(metin)) continue;

                if (!sonuc.TryGetValue(pid, out var liste))
                    sonuc[pid] = liste = new List<CardMessageItem>();
                liste.Add(new CardMessageItem(m.Slot, metin, m.Icon, m.Color));
            }
        }
        return sonuc;
    }
}
