namespace ECSPros.Shared.Contracts;

/// <summary>Kargo hesabının sonucu: tahsil edilecek bedel + bedavaysa sebebi + kampanya etkisi.</summary>
public readonly record struct KargoSonucu(decimal Ucret, string? BedavaSebebi, bool KampanyaEtkili);

/// <summary>
/// Kargo ücreti TEK kural (2026-09-09, kullanıcı kararları): kanal başına sabit ücret +
/// kanal ücretsiz-kargo eşiği; kargo kampanyası ücreti düşürür/kaldırır.
///
/// Kararlar:
///  • Ücret modeli: kanal başına tek sabit ücret (FirmPlatform.Settings → shippingFee).
///  • Eşik tabanı: İNDİRİMLER SONRASI ödenecek ürün tutarı (kupon + kampanya düşülmüş).
///  • Öncelik: kampanya yalnız ücreti düşürür; koşulu tutmazsa kanal ayarı normal işler
///    (kanal eşiği de bedava yapabilir) — iki sonuçtan MÜŞTERİ LEHİNE olanı uygulanır.
///
/// Sepet/teslimat/ödeme ekranları bu kuralın sonucunu GÖSTERİR; tahsil edilen tutar checkout'ta
/// yine bu kuralla SUNUCUDA hesaplanır (istemciden kargo tutarı alınmaz).
/// </summary>
public static class KargoUcretiKurali
{
    public const string SebepKampanya = "campaign";   // kargo kampanyası bedava yaptı
    public const string SebepEsik     = "threshold";  // kanal ücretsiz kargo eşiği aşıldı
    public const string SebepUcretsiz = "none";       // kanalda kargo ücreti tanımlı değil

    /// <param name="kanalUcret">Kanalın sabit kargo bedeli (0 = kargo ücretsiz).</param>
    /// <param name="kanalEsik">Kanalın ücretsiz kargo eşiği (0 = eşik yok).</param>
    /// <param name="odenecekUrunTutari">İndirimler sonrası ürün tutarı (kargo hariç).</param>
    /// <param name="kampanyaUcreti">Kargo kampanyasının hesapladığı bedel; kampanya yoksa/uymuyorsa null.</param>
    public static KargoSonucu Hesapla(
        decimal kanalUcret, decimal kanalEsik, decimal odenecekUrunTutari, decimal? kampanyaUcreti)
    {
        if (kanalUcret <= 0)
            return new KargoSonucu(0m, SebepUcretsiz, false);

        var esikBedava = kanalEsik > 0 && odenecekUrunTutari >= kanalEsik;
        var kanalSonuc = esikBedava ? 0m : Math.Round(kanalUcret, 2);

        // Kampanya yalnız düşürebilir: iki sonuçtan müşteri lehine olan uygulanır.
        var kampanyaSonuc = kampanyaUcreti.HasValue
            ? Math.Clamp(Math.Round(kampanyaUcreti.Value, 2), 0m, Math.Round(kanalUcret, 2))
            : (decimal?)null;

        var ucret = kampanyaSonuc.HasValue ? Math.Min(kanalSonuc, kampanyaSonuc.Value) : kanalSonuc;
        var kampanyaEtkili = kampanyaSonuc.HasValue && kampanyaSonuc.Value < kanalSonuc;

        if (ucret > 0)
            return new KargoSonucu(ucret, null, kampanyaEtkili);

        // Bedava: sebep hangi kuraldan geldi? (ikisi de sıfırlıyorsa kampanya öne yazılır)
        var sebep = kampanyaSonuc is 0m ? SebepKampanya : SebepEsik;
        return new KargoSonucu(0m, sebep, kampanyaSonuc is 0m);
    }

    /// <summary>Ücretsiz kargoya kalan tutar (eşik yoksa / zaten bedavaysa null) — "X TL daha ekleyin" metni.</summary>
    public static decimal? EsigeKalan(
        decimal kanalUcret, decimal kanalEsik, decimal odenecekUrunTutari, decimal? kampanyaUcreti)
    {
        if (kanalUcret <= 0 || kanalEsik <= 0) return null;
        if (kampanyaUcreti is 0m) return null;               // kampanya zaten bedava yaptı
        var kalan = kanalEsik - odenecekUrunTutari;
        return kalan > 0 ? Math.Round(kalan, 2) : null;
    }
}
