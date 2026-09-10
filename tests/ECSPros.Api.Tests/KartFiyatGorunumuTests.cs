using ECSPros.Api.Services.Store;
using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using ECSPros.Shared.Contracts;

namespace ECSPros.Api.Tests;

/// <summary>
/// B9 (2026-09-08, mobil ekip isteği): vitrin tek fiyat sözleşmesi. İndirim iki kaynaktan gelir (kanal fiyat indirimi ve
/// ürün-bazlı kampanya) ve üst üste binebilir; sepet-bağımlı kampanyalar fiyata dokunmaz. Kural tek yerdedir, istemci yorumlamaz.
/// Çizili fiyat salt gösterimdir (kullanıcı kararı) → kampanya varken en yüksek gerçek referans gösterilir.
/// </summary>
[TestClass]
public sealed class KartFiyatGorunumuTests
{
    [TestMethod]
    public void Indirim_yoksa_cizili_fiyat_null()
    {
        Assert.AreEqual((100m, (decimal?)null), KartFiyatGorunumu.Hesapla(100m, null, null));
        // sepet-bağımlı kampanya (2 al 1 öde / kargo bedava): rozet gelir ama kampanyalı fiyat yoktur
        Assert.AreEqual((100m, (decimal?)null), KartFiyatGorunumu.Hesapla(100m, null, kampanyaFiyat: null));
    }

    // 2026-09-10 (kullanıcı kararı): bedenler farklı fiyatlıysa kart EN YÜKSEK fiyatı gösterir; kural varyant
    // başına efektif fiyat (kanal ?? taban) üzerinden çalışır — kanal fiyatlarının ayrı maks'ı DEĞİL.
    [TestMethod]
    public void Kart_taban_fiyati_bedenler_arasinda_en_yuksek_efektif_fiyattir()
    {
        // P-00020386 canlı vakası: tek kanal fiyatı stoksuz L'de 299,99; S/M tabandan 399,99'a satılıyor → kart 399,99
        Assert.AreEqual(399.99m, KartFiyatGorunumu.KartTabanFiyati([(299.99m, 399.99m), (0m, 399.99m), (0m, 399.99m)]));
        // tüm bedenlerde kanal fiyatı var ve farklı → en yüksek kanal fiyatı
        Assert.AreEqual(349.99m, KartFiyatGorunumu.KartTabanFiyati([(299.99m, 399.99m), (349.99m, 399.99m)]));
        // hiç kanal fiyatı yok → en yüksek taban
        Assert.AreEqual(450m, KartFiyatGorunumu.KartTabanFiyati([(0m, 400m), (0m, 450m)]));
        // kanal fiyatı tabandan yüksek olabilir (kanal zammı) → o da sayılır
        Assert.AreEqual(500m, KartFiyatGorunumu.KartTabanFiyati([(500m, 400m), (0m, 450m)]));
        // olumsuz: hiç pozitif fiyat yok → 0 (tüketici ürün BasePrice'ına düşer)
        Assert.AreEqual(0m, KartFiyatGorunumu.KartTabanFiyati([(0m, 0m)]));
        Assert.AreEqual(0m, KartFiyatGorunumu.KartTabanFiyati([]));
    }

    [TestMethod]
    public void Kanal_indiriminde_satis_fiyati_dusuk_cizili_kanal_fiyatidir()
        => Assert.AreEqual((299.99m, (decimal?)399.99m), KartFiyatGorunumu.Hesapla(299.99m, 399.99m, null));

    [TestMethod]
    public void Kampanyada_satis_fiyati_kampanyali_cizili_kampanya_oncesi_fiyattir()
        => Assert.AreEqual((679.99m, (decimal?)799.99m), KartFiyatGorunumu.Hesapla(799.99m, null, 679.99m));

    [TestMethod]
    public void Kanal_indirimi_ve_kampanya_birlikteyse_cizili_en_yuksek_referanstir()
        => Assert.AreEqual((254.99m, (decimal?)399.99m), KartFiyatGorunumu.Hesapla(299.99m, 399.99m, 254.99m));

    [TestMethod]
    public void Gecersiz_degerler_indirim_uretmez()
    {
        Assert.AreEqual((100m, (decimal?)null), KartFiyatGorunumu.Hesapla(100m, null, 100m), "kampanya fiyatı satış fiyatına eşit");
        Assert.AreEqual((100m, (decimal?)null), KartFiyatGorunumu.Hesapla(100m, null, 120m), "kampanya fiyatı daha yüksek");
        Assert.AreEqual((100m, (decimal?)null), KartFiyatGorunumu.Hesapla(100m, null, 0m), "kampanya fiyatı sıfır");
        Assert.AreEqual((100m, (decimal?)null), KartFiyatGorunumu.Hesapla(100m, 100m, null), "çizili fiyat satış fiyatına eşit");
        Assert.AreEqual((100m, (decimal?)null), KartFiyatGorunumu.Hesapla(100m, 90m, null), "çizili fiyat satış fiyatının altında");
    }

    [TestMethod]
    public void Kural_idempotenttir()
    {
        // Kategori listesi cache'ten okunup her seferinde yeniden zenginleştirildiği için kural kendi çıktısına
        // yeniden uygulanabilir; sonuç değişmemeli.
        var (fiyat1, cizili1) = KartFiyatGorunumu.Hesapla(299.99m, 399.99m, 254.99m);
        var (fiyat2, cizili2) = KartFiyatGorunumu.Hesapla(299.99m, cizili1, 254.99m);
        Assert.AreEqual((fiyat1, cizili1), (fiyat2, cizili2));

        var (f3, c3) = KartFiyatGorunumu.Hesapla(799.99m, null, 679.99m);
        var (f4, c4) = KartFiyatGorunumu.Hesapla(799.99m, c3, 679.99m);
        Assert.AreEqual((f3, c3), (f4, c4));
    }

    [TestMethod]
    public void Web_kartinin_cizili_satiri_bugunku_davranisini_korur()
    {
        // UrunKartMap: EskiFiyat = CompareAtPrice > satış fiyatı(MinPrice) ise. Kampanya kaynaklı referans web'de çizgi ÜRETMEZ
        // (kampanyanın kendi satırı var) — kanal indirimi üretir.
        var (_, kampanyaCizili) = KartFiyatGorunumu.Hesapla(799.99m, null, 679.99m);
        Assert.IsFalse(kampanyaCizili is { } k1 && k1 > 799.99m, "kampanya referansı MinPrice'a eşit → web çizgi göstermez");

        var (_, kanalCizili) = KartFiyatGorunumu.Hesapla(299.99m, 399.99m, null);
        Assert.IsTrue(kanalCizili is { } k2 && k2 > 299.99m, "kanal indirimi → web çizgi gösterir");
    }

    [TestMethod]
    public void Uye_listesi_ozeti_kartin_rozetlerini_ve_fiyatini_tasir()
    {
        // Favoriler/gezilenler/koleksiyonlar bu izdüşümü kullanır; B9 öncesi rozetler burada düşüyordu.
        var kart = new StoreProductDto(
            Guid.NewGuid(), "P-00021945", new Dictionary<string, string> { ["tr"] = "Elbise" }, null,
            "https://cdn/x.webp", 799.99m, 799.99m, true, [], [],
            CampaignName: "%15 İndirim",
            CampaignPrice: 679.99m,
            CampaignBadges: [new CampaignBadge("%15 İndirim", null), new CampaignBadge("Kargo Bedava", "#16A34A")],
            Price: 679.99m);

        var ozet = StoreKartZenginlestirici.Ozet(new Dictionary<string, StoreProductDto> { ["P-00021945"] = kart }, "P-00021945");

        Assert.IsNotNull(ozet);
        Assert.AreEqual(679.99m, ozet!.Price, "satış fiyatı kampanyalı");
        Assert.AreEqual(799.99m, ozet.CompareAtPrice, "çizili referans");
        Assert.AreEqual("%15 İndirim", ozet.CampaignName);
        Assert.AreEqual(2, ozet.CampaignBadges?.Count, "rozet bandı satıra taşınır");
        Assert.AreEqual("Kargo Bedava", ozet.CampaignBadges![1].Name);
    }
}
