using ECSPros.Shared.Contracts;

namespace ECSPros.Api.Tests;

/// <summary>
/// 2026-09-10 (kullanıcı kararları): taksitlendirmenin MÜŞTERİ tarafı kanalın kendi tablosundan gelir.
/// Vade farkı ürünlere yedirilmez, KDV oranına göre oranlanıp ayrı satır olur; iadede kalem payı müşteriye ödenir.
/// Kural tek yerde (TaksitKurali) — ödeme sayfası, ödeme başlatma, fatura ve iade aynı hesabı kullanır.
/// </summary>
[TestClass]
public sealed class TaksitKuraliTests
{
    private static readonly TaksitTablosuSatiri[] Mishar =
    [
        new(2, 8.40m), new(3, 10.44m), new(4, 12.47m), new(5, 14.52m), new(6, 16.55m)
    ];

    [TestMethod]
    public void Tek_cekim_her_zaman_ilk_secenektir_ve_vade_farksizdir()
    {
        var s = TaksitKurali.Secenekler(1000m, Mishar);
        Assert.AreEqual(6, s.Count);
        Assert.AreEqual(new TaksitSecenegiSonucu(1, 1000m, 1000m, 0m), s[0]);
    }

    [TestMethod]
    public void Vade_farki_yuzdeyle_toplama_eklenir_aylik_gosterimdir()
    {
        var s = TaksitKurali.Secenekler(1000m, Mishar);
        var iki = s.Single(x => x.Adet == 2);
        Assert.AreEqual(1084.00m, iki.Toplam);
        Assert.AreEqual(542.00m, iki.Birim);
        Assert.AreEqual(84.00m, iki.VadeFarki);
        var alti = s.Single(x => x.Adet == 6);
        Assert.AreEqual(1165.50m, alti.Toplam);
        Assert.AreEqual(194.25m, alti.Birim);
    }

    [TestMethod]
    public void Sifir_yuzde_vade_farksiz_taksittir()
    {
        // Tozlu eski tablo: 2 ve 3 taksit vade farksız
        var s = TaksitKurali.Secenekler(299.99m, [new(2, 0m), new(3, 0m)]);
        Assert.AreEqual(299.99m, s.Single(x => x.Adet == 3).Toplam);
        Assert.AreEqual(0m, s.Single(x => x.Adet == 3).VadeFarki);
        Assert.AreEqual(100.00m, s.Single(x => x.Adet == 3).Birim);   // 99.9966 → 100.00 (salt gösterim)
    }

    [TestMethod]
    public void Gecersiz_satirlar_atlanir_ayni_adette_son_yazilan_kazanir()
    {
        var s = TaksitKurali.Secenekler(100m, [new(1, 5m), new(13, 5m), new(2, -1m), new(3, 5m), new(3, 10m)]);
        Assert.AreEqual(2, s.Count);                       // tek çekim + 3 taksit
        Assert.AreEqual(110m, s[1].Toplam);
    }

    [TestMethod]
    public void Tabloda_olmayan_taksit_reddedilir_tek_cekim_sifirdir()
    {
        Assert.IsNull(TaksitKurali.VadeFarki(1000m, 9, Mishar));
        Assert.AreEqual(0m, TaksitKurali.VadeFarki(1000m, 1, Mishar));
        Assert.AreEqual(84.00m, TaksitKurali.VadeFarki(1000m, 2, Mishar));
        // olumsuz: tablo yok / baz sıfır → yalnız tek çekim
        Assert.AreEqual(1, TaksitKurali.Secenekler(1000m, null).Count);
        Assert.AreEqual(1, TaksitKurali.Secenekler(0m, Mishar).Count);
    }

    [TestMethod]
    public void Kalem_paylari_tutar_oraninda_ve_kurus_farki_kaybolmaz()
    {
        var paylar = TaksitKurali.KalemPaylari(10.00m, [("a", 100m), ("b", 100m), ("c", 100m)]);
        Assert.AreEqual(10.00m, paylar.Values.Sum());
        Assert.IsTrue(paylar.Values.All(p => p is 3.33m or 3.34m));
        // sıfır tutarlı kalem pay almaz; vade farkı yoksa hepsi 0
        Assert.AreEqual(0m, TaksitKurali.KalemPaylari(10m, [("a", 0m), ("b", 50m)])["a"]);
        Assert.IsTrue(TaksitKurali.KalemPaylari(0m, [("a", 100m)]).Values.All(p => p == 0m));
    }

    [TestMethod]
    public void Kdv_satirlari_urunlerin_oranina_gore_ayrilir_iki_oran_iki_satir()
    {
        // 600 TL %20'lik + 400 TL %10'luk ürün; vade farkı 100 TL → 60 / 40 brüt paylaşım
        var satirlar = TaksitKurali.KdvSatirlari(100m, [(600m, 20m), (400m, 10m)]);
        Assert.AreEqual(2, satirlar.Count);
        var on = satirlar.Single(s => s.KdvOrani == 10m);
        var yirmi = satirlar.Single(s => s.KdvOrani == 20m);
        Assert.AreEqual(40.00m, on.Brut);  Assert.AreEqual(36.36m, on.Net);  Assert.AreEqual(3.64m, on.Kdv);
        Assert.AreEqual(60.00m, yirmi.Brut); Assert.AreEqual(50.00m, yirmi.Net); Assert.AreEqual(10.00m, yirmi.Kdv);
        Assert.AreEqual(100.00m, satirlar.Sum(s => s.Brut));
    }

    [TestMethod]
    public void Kdv_satirlari_tek_oranda_tek_satir_vade_farki_yoksa_bos()
    {
        var tek = TaksitKurali.KdvSatirlari(84.00m, [(600m, 10m), (400m, 10m)]);
        Assert.AreEqual(1, tek.Count);
        Assert.AreEqual(84.00m, tek[0].Brut);
        Assert.AreEqual(76.36m, tek[0].Net);
        Assert.AreEqual(0, TaksitKurali.KdvSatirlari(0m, [(600m, 10m)]).Count);
        Assert.AreEqual(0, TaksitKurali.KdvSatirlari(50m, []).Count);
    }
}
