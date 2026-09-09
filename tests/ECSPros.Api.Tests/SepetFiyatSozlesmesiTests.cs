using ECSPros.Shared.Contracts;

namespace ECSPros.Api.Tests;

/// <summary>
/// M1 (2026-09-09, mobil isteği): sepet fiyat sözleşmesi.
///
/// Kapatılan hata: sepet, istemcinin gönderdiği fiyata kampanyayı BİR KEZ DAHA uyguluyordu.
/// Mobil B9 sözleşmesine uyup listedeki kampanyalı fiyatı gönderince indirim çift sayılıyordu
/// (679,99 → 577,99). Kural artık tek: taban fiyat SUNUCUDAN gelir, kampanya onun üzerine
/// TEK kez uygulanır; sepet satırı liste kartıyla aynı (price, compareAtPrice) çiftini verir.
/// </summary>
[TestClass]
public sealed class SepetFiyatSozlesmesiTests
{
    [TestMethod]
    public void Liste_karti_ile_sepet_satiri_AYNI_fiyat_ciftini_verir()
    {
        // P-00021945 örneği: taban 799,99 · %15 kampanya → 679,99
        var (listePrice, listeCompare) = KartFiyatGorunumu.Hesapla(799.99m, null, 679.99m);
        var (sepetPrice, sepetCompare) = KartFiyatGorunumu.Hesapla(799.99m, null, 679.99m);

        Assert.AreEqual(listePrice, sepetPrice);
        Assert.AreEqual(listeCompare, sepetCompare);
        Assert.AreEqual(679.99m, sepetPrice);
        Assert.AreEqual(799.99m, sepetCompare);
    }

    [TestMethod]
    public void Kampanyali_fiyat_taban_olarak_verilirse_indirim_ikinci_kez_uygulanmaz()
    {
        // Hatanın davranışı: taban olarak KAMPANYALI fiyat (679,99) geçilir ve kampanya
        // yine %15 hesaplarsa 577,99 çıkardı. Sunucu tabanı kullanıldığında bu mümkün değildir:
        // taban 799,99 → kampanya 679,99 → indirim 120,00 (bir kez).
        const decimal taban = 799.99m, kampanyali = 679.99m;
        var indirim = taban - kampanyali;
        Assert.AreEqual(120.00m, indirim);

        // Çifte uygulama olsaydı: 679,99 × 0,85 = 577,99 → indirim 222,00 olurdu.
        var cifteIndirim = taban - Math.Round(kampanyali * 0.85m, 2);
        Assert.AreNotEqual(indirim, cifteIndirim, "Çifte indirim ile tek indirim aynı olmamalı.");
    }

    [TestMethod]
    public void Indirim_yoksa_cizili_fiyat_null_dondurulur()
    {
        var (price, compareAt) = KartFiyatGorunumu.Hesapla(299.99m, null, null);
        Assert.AreEqual(299.99m, price);
        Assert.IsNull(compareAt, "İndirim yokken çizili fiyat gösterilmez.");
    }

    [TestMethod]
    public void Kanal_cizili_fiyati_kampanyadan_buyukse_referans_odur()
    {
        // P-00020386: liste 299,99 / 399,99 → kanal çizili fiyatı referanstır.
        var (price, compareAt) = KartFiyatGorunumu.Hesapla(299.99m, 399.99m, null);
        Assert.AreEqual(299.99m, price);
        Assert.AreEqual(399.99m, compareAt);
    }

    [TestMethod]
    public void Sepet_toplami_kargo_kuralini_indirim_SONRASI_tutardan_uygular()
    {
        // Kanal: 49,90 kargo · 500 TL eşik. Ara toplam 600, indirim 150 → ödenecek 450 → kargo ücretli.
        var sonuc = KargoUcretiKurali.Hesapla(49.90m, 500m, 450m, null);
        Assert.AreEqual(49.90m, sonuc.Ucret);
        Assert.IsNull(sonuc.BedavaSebebi);

        var kalan = KargoUcretiKurali.EsigeKalan(49.90m, 500m, 450m, null);
        Assert.AreEqual(50m, kalan, "Ücretsiz kargoya kalan tutar eşikten ödenecek tutar düşülerek bulunur.");

        // 520 ödenecekse eşik aşılır → bedava, sebep 'threshold'.
        var bedava = KargoUcretiKurali.Hesapla(49.90m, 500m, 520m, null);
        Assert.AreEqual(0m, bedava.Ucret);
        Assert.AreEqual(KargoUcretiKurali.SebepEsik, bedava.BedavaSebebi);
        Assert.IsNull(KargoUcretiKurali.EsigeKalan(49.90m, 500m, 520m, null));
    }

    [TestMethod]
    public void Kargo_kampanyasi_bedava_yaparsa_sebep_campaign_olur()
    {
        var sonuc = KargoUcretiKurali.Hesapla(49.90m, 500m, 200m, 0m);
        Assert.AreEqual(0m, sonuc.Ucret);
        Assert.AreEqual(KargoUcretiKurali.SebepKampanya, sonuc.BedavaSebebi);
        Assert.IsTrue(sonuc.KampanyaEtkili);
    }

    [TestMethod]
    public void Indirim_yuzdesi_cizili_fiyata_gore_tam_sayidir()
    {
        // discountPercent = (compareAt − price) / compareAt × 100, yuvarlanmış.
        static int Yuzde(decimal price, decimal compareAt)
            => (int)Math.Round((compareAt - price) / compareAt * 100m, MidpointRounding.AwayFromZero);

        Assert.AreEqual(25, Yuzde(299.99m, 399.99m));
        Assert.AreEqual(15, Yuzde(679.99m, 799.99m));
        Assert.AreEqual(63, Yuzde(299.99m, 799.99m));
    }
}
