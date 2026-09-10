namespace ECSPros.Shared.Contracts;

/// <summary>
/// Vitrin fiyat sözleşmesi (B9, 2026-09-08 — mobil ekip isteği): kart, detay ve üye listeleri TEK biçimde fiyat verir.
/// <c>price</c> = satış fiyatı (ürün-bazlı kampanya varsa kampanyalı), <c>compareAtPrice</c> = çizili referans (indirim yoksa null).
///
/// İndirim üç kaynaktan gelebilir ve ikisi fiyatı değiştirir:
///  • kanal fiyat indirimi → satış fiyatı zaten düşüktür, kanal <c>CompareAtPrice</c>'ı çizili referanstır,
///  • ürün-bazlı kampanya (percent/amount) → satış fiyatının ÜSTÜNE uygulanır (<c>CampaignPricing.EffectivePrice</c>),
///  • sepet-bağımlı kampanya (2 al 1 öde, kargo bedava) → fiyata dokunmaz, yalnız rozet üretir.
/// İkisi aynı üründe üst üste binebilir; o zaman satış fiyatı kampanyalı fiyattır.
///
/// Çizili fiyat SALT GÖSTERİMDİR (2026-09-08 kullanıcı kararı: "hiçbir hesaba ya da karara etkisi yok"), bu yüzden
/// kampanya varken en yüksek gerçek referans gösterilir: kanal çizili fiyatı ya da kampanya öncesi satış fiyatı.
/// Kural TEK yerde durur ki istemci (mobil/web) yorum yapmak zorunda kalmasın.
/// </summary>
public static class KartFiyatGorunumu
{
    /// <summary>
    /// Kartın kampanya öncesi satış fiyatı (2026-09-10 kullanıcı kararı: bedenler farklı fiyatlıysa EN YÜKSEK).
    /// Önce her varyantın EFEKTİF fiyatı bulunur (kanal fiyatı > 0 ise kanal, yoksa taban — sepet/checkout'un
    /// varyant başına uyguladığı kuralın aynısı), sonra bunların en yükseği alınır; pozitif fiyat yoksa 0.
    /// ★ Kanal fiyatları ve taban fiyatlar AYRI AYRI maks'lanıp "kanal varsa kanal" denmez: kanal fiyatı
    /// çoğu üründe bedenlerin yalnız bir kısmında vardır (2026-09-10 canlı: 22.5K üründe kısmi) — P-00020386'da
    /// tek kanal fiyatı stoksuz L'de 299,99 iken S/M tabandan 399,99'a satılıyordu ve kart 299,99 gösteriyordu.
    /// </summary>
    /// <param name="varyantlar">Varyant başına (kanal fiyatı, taban fiyat); olmayan değer 0.</param>
    public static decimal KartTabanFiyati(IEnumerable<(decimal Kanal, decimal Taban)> varyantlar)
        => varyantlar.Select(v => v.Kanal > 0 ? v.Kanal : v.Taban).Where(p => p > 0).DefaultIfEmpty(0m).Max();

    /// <param name="minPrice">Kampanya öncesi satış fiyatı (kanal fiyatı → varyant; listede bedenler arasında EN YÜKSEK pozitif, 2026-09-10).</param>
    /// <param name="kanalCizili">Kanal <c>CompareAtPrice</c>'ı (yoksa null).</param>
    /// <param name="kampanyaFiyat">Ürün-bazlı kampanyalı fiyat (yoksa/sepet-bağımlıysa null).</param>
    /// <returns>(satış fiyatı, çizili referans — indirim yoksa null).</returns>
    public static (decimal Price, decimal? CompareAtPrice) Hesapla(decimal minPrice, decimal? kanalCizili, decimal? kampanyaFiyat)
    {
        var price = kampanyaFiyat is { } k && k > 0 && k < minPrice ? k : minPrice;
        var referans = Math.Max(kanalCizili ?? 0m, price < minPrice ? minPrice : 0m);
        return (price, referans > price ? referans : null);
    }
}
