using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts;

namespace ECSPros.Api.Models.Store;

/// <summary>
/// Store DTO'larını kart görünüm modeline çevirir. B7'de UrunListesiController içindeydi;
/// B6'da ana sayfa carousel'leri de aynı dönüşümü kullandığı için buraya alındı.
/// </summary>
public static class UrunKartMap
{
    public static UrunKartVm KartaCevir(ChannelCategoryProductItemDto p)
    {
        var renkler = p.Colors ?? [];
        var degerIdler = renkler.Select(c => c.ValueId)
            .Concat((p.Attrs ?? []).Select(a => a.ValueId))
            .Distinct().ToList();
        // Tooltip eksen (renk) kartlarından: kategori kartı zaten renk başına — diğer renk
        // kartlarına ?color={eksenDeğeri} ile gider. Eksen verisi yoksa filtre_rengi'ne düş.
        // Görselsiz renkler listelenmez (B9 kuralı) — detay onları zaten göstermez,
        // linklenirse ilk görünür renge düşer ve kullanıcı yanılır.
        var secenekler = (p.AxisColors is { Count: > 0 } eksen ? eksen : renkler)
            .Where(c => c.ImageUrl is not null)
            .Select(c => new KartRenkVm(c.ValueId, TrAd(c.NameI18n), c.ImageUrl, c.Slug))  // 2b: renk slug'ı
            .ToList();
        return new UrunKartVm(
            p.Code, TrAd(p.NameI18n), p.MainImageUrl, p.BasePrice,
            renkler.Where(c => c.HexCode is not null).Select(c => c.HexCode!).Take(2).ToList(),
            secenekler.Count > 0 ? secenekler.Count : renkler.Count, degerIdler,
            p.GalleryUrls ?? [], secenekler, p.SelectedColorValueId, p.IsFeatured, p.Rating, p.ReviewCount,
            Slug: p.Slug);   // 2b: kartın seçili renk slug'ı
    }

    public static UrunKartVm KartaCevir(StoreProductDto p)
    {
        var degerIdler = p.Colors.Select(c => c.ValueId)
            .Concat(p.Attrs.Select(a => a.ValueId))
            .Distinct().ToList();
        // Görselsiz renkler tooltip'te listelenmez (B9 kuralı — yukarıdaki nota bak)
        var secenekler = p.Colors
            .Where(c => c.ImageUrl is not null)
            .Select(c => new KartRenkVm(c.ValueId, TrAd(c.NameI18n), c.ImageUrl))
            .ToList();
        // Kabul testi 2026-07-22: aramadaki renk kelimesiyle eşleşen renk varsa kart o
        // renkle gösterilir ("kırmızı elbise" kartında kırmızı görsel/detay linki).
        var eslesenRenk = p.MatchedColorValueId is { } mid
            ? p.Colors.FirstOrDefault(c => c.ValueId == mid) : null;
        // 2026-07-31: kartın detay linki GÖSTERDİĞİ renge gitsin — aramada eşleşen renk yoksa
        // ana görselin rengi (MainColorValueId). Böylece "son baktıklarım"/carousel kartında
        // görülen renk (ör. bej) tıklanınca detayda da o renk açılır (renksiz link stoklu-varsayılana
        // düşüyordu → liste ≠ detay). SeciliRenkId → UrunKartVm.Url'de ?color= olarak taşınır.
        var kartRenkId = eslesenRenk?.ValueId ?? p.MainColorValueId;
        return new UrunKartVm(
            p.Code, TrAd(p.NameI18n), eslesenRenk?.ImageUrl ?? p.MainImageUrl, p.MinPrice,
            p.Colors.Where(c => c.HexCode is not null).Select(c => c.HexCode!).Take(2).ToList(),
            secenekler.Count > 0 ? secenekler.Count : p.Colors.Count, degerIdler,
            p.GalleryUrls ?? [], secenekler, kartRenkId, p.IsFeatured, p.Rating, p.ReviewCount,
            p.VideoUrl,
            Slug: eslesenRenk?.Slug); // eşleşen rengin gerçek slug'ı (varsa)
    }

    public static string TrAd(Dictionary<string, string> nameI18n) =>
        nameI18n.TryGetValue("tr", out var ad) ? ad : nameI18n.Values.FirstOrDefault() ?? "";
}
