using ECSPros.Order.Application.Services;
using ECSPros.Shared.Contracts;

namespace ECSPros.Api.Tests;

/// <summary>
/// M2 (2026-09-09): sipariş tutarının aritmetiği TEK kuralda — <c>POST /checkout</c> ile
/// <c>POST /checkout/preview</c> aynı sayıyı üretmek zorunda. Ön izlemede 1.000 TL görüp
/// siparişte 1.049 TL ödemek güveni bitirir; bu testler kuralın iki yolda da aynı kalmasını
/// ve kenar durumlarda (indirim > ara toplam, kapıda ödeme, kargo eşiği) doğru davranmasını sabitler.
/// </summary>
[TestClass]
public sealed class SiparisTutarKuraliTests
{
    [TestMethod]
    public void Genel_toplam_aratoplam_eksi_indirim_arti_masraf_arti_kargodur()
    {
        var t = SiparisTutarKurali.Hesapla(
            subtotal: 1000m, kuponIndirimi: 100m, kampanyaSepetIndirimi: 50m,
            kapidaOdeme: false, kapidaOdemeMasrafi: 25m,
            kanalKargoUcreti: 49.90m, kanalKargoEsigi: 0m, kargoKampanyaUcreti: null);

        Assert.AreEqual(150m, t.Indirim);
        Assert.AreEqual(0m, t.Masraf, "Kapıda ödeme değilse masraf eklenmez.");
        Assert.AreEqual(49.90m, t.KargoBedeli);
        Assert.AreEqual(899.90m, t.GenelToplam);   // 1000 − 150 + 0 + 49,90
    }

    [TestMethod]
    public void Indirim_ara_toplami_asamaz()
    {
        // Kupon + kampanya birlikte sepetten büyükse sipariş EKSİYE düşmemeli.
        var t = SiparisTutarKurali.Hesapla(200m, 300m, 100m, false, 0m, 0m, 0m, null);
        Assert.AreEqual(200m, t.Indirim);
        Assert.AreEqual(0m, t.GenelToplam);
    }

    [TestMethod]
    public void Kapida_odemede_hizmet_bedeli_eklenir()
    {
        var t = SiparisTutarKurali.Hesapla(500m, 0m, 0m, true, 39m, 0m, 0m, null);
        Assert.AreEqual(39m, t.Masraf);
        Assert.AreEqual(539m, t.GenelToplam);
    }

    [TestMethod]
    public void Kargo_esigi_INDIRIM_SONRASI_tutara_bakar()
    {
        // 600 TL sepet, 150 TL indirim → ödenecek 450 → 500 TL eşiği GEÇMEZ, kargo ücretli.
        var ucretli = SiparisTutarKurali.Hesapla(600m, 150m, 0m, false, 0m, 49.90m, 500m, null);
        Assert.AreEqual(49.90m, ucretli.KargoBedeli);
        Assert.IsNull(ucretli.KargoBedavaSebebi);

        // İndirim olmadan aynı sepet eşiği geçer → bedava.
        var bedava = SiparisTutarKurali.Hesapla(600m, 0m, 0m, false, 0m, 49.90m, 500m, null);
        Assert.AreEqual(0m, bedava.KargoBedeli);
        Assert.AreEqual(KargoUcretiKurali.SebepEsik, bedava.KargoBedavaSebebi);
    }

    [TestMethod]
    public void Kargo_kampanyasi_yalniz_dusurur_musteri_lehine_sonuc_kazanir()
    {
        // Kampanya 30 TL diyor ama kanal eşiği zaten bedava yapıyorsa BEDAVA kalır.
        var t = SiparisTutarKurali.Hesapla(600m, 0m, 0m, false, 0m, 49.90m, 500m, 30m);
        Assert.AreEqual(0m, t.KargoBedeli);

        // Eşik tutmuyorsa kampanyanın düşürdüğü bedel geçerli.
        var t2 = SiparisTutarKurali.Hesapla(300m, 0m, 0m, false, 0m, 49.90m, 500m, 30m);
        Assert.AreEqual(30m, t2.KargoBedeli);
        Assert.AreEqual(330m, t2.GenelToplam);
    }

    [TestMethod]
    public void Kanalda_kargo_ucreti_yoksa_bedel_sifirdir()
    {
        var t = SiparisTutarKurali.Hesapla(100m, 0m, 0m, false, 0m, 0m, 0m, null);
        Assert.AreEqual(0m, t.KargoBedeli);
        Assert.AreEqual(KargoUcretiKurali.SebepUcretsiz, t.KargoBedavaSebebi);
        Assert.AreEqual(100m, t.GenelToplam);
    }
}
