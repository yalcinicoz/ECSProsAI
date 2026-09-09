using ECSPros.Shared.Contracts;

namespace ECSPros.Api.Tests;

/// <summary>
/// 2026-09-09: kanal bazlı kargo ücreti + ücretsiz kargo eşiği. Kullanıcı kararları:
/// kanal başına TEK sabit ücret, eşik tabanı İNDİRİMLER SONRASI ödenecek tutar,
/// kargo kampanyası yalnız ücreti düşürür (iki sonuçtan müşteri lehine olan uygulanır).
/// Regresyon: kargo bedeli hiç hesaplanmıyordu — sepette sabit "Ücretsiz" yazıyordu.
/// </summary>
[TestClass]
public sealed class KargoUcretiKuraliTests
{
    [TestMethod]
    public void Kanalda_ucret_tanimli_degilse_ucretsiz()
    {
        var s = KargoUcretiKurali.Hesapla(0m, 0m, 59.99m, null);
        Assert.AreEqual(0m, s.Ucret);
        Assert.AreEqual(KargoUcretiKurali.SebepUcretsiz, s.BedavaSebebi);
    }

    [TestMethod]
    public void Esik_altinda_kargo_tahsil_edilir()
    {
        var s = KargoUcretiKurali.Hesapla(59.99m, 1000m, 59.99m, null);
        Assert.AreEqual(59.99m, s.Ucret);
        Assert.IsNull(s.BedavaSebebi);
    }

    [TestMethod]
    public void Esik_asilinca_kargo_bedava()
    {
        var s = KargoUcretiKurali.Hesapla(59.99m, 1000m, 1000m, null);   // eşik DAHİL
        Assert.AreEqual(0m, s.Ucret);
        Assert.AreEqual(KargoUcretiKurali.SebepEsik, s.BedavaSebebi);
    }

    [TestMethod]
    public void Esik_yoksa_her_tutarda_ucret_alinir()
    {
        Assert.AreEqual(59.99m, KargoUcretiKurali.Hesapla(59.99m, 0m, 5000m, null).Ucret);
    }

    [TestMethod]
    public void Kampanya_ucreti_kaldirir()
    {
        var s = KargoUcretiKurali.Hesapla(59.99m, 1000m, 800m, 0m);
        Assert.AreEqual(0m, s.Ucret);
        Assert.AreEqual(KargoUcretiKurali.SebepKampanya, s.BedavaSebebi);
        Assert.IsTrue(s.KampanyaEtkili);
    }

    [TestMethod]
    public void Kampanya_kismi_indirim_uygulayabilir()
    {
        var s = KargoUcretiKurali.Hesapla(60m, 1000m, 500m, 20m);   // kampanya 20 TL'ye indirdi
        Assert.AreEqual(20m, s.Ucret);
        Assert.IsTrue(s.KampanyaEtkili);
        Assert.IsNull(s.BedavaSebebi);
    }

    [TestMethod]
    public void Kanal_esigi_kampanyadan_iyiyse_kanal_kazanir()
    {
        // Kampanya 20 TL'ye indiriyor ama kanal eşiği zaten bedava yapıyor → 0
        var s = KargoUcretiKurali.Hesapla(60m, 500m, 800m, 20m);
        Assert.AreEqual(0m, s.Ucret);
        Assert.AreEqual(KargoUcretiKurali.SebepEsik, s.BedavaSebebi);
        Assert.IsFalse(s.KampanyaEtkili);
    }

    [TestMethod]
    public void Kampanya_ucreti_kanal_ucretini_asamaz()
    {
        var s = KargoUcretiKurali.Hesapla(50m, 0m, 100m, 90m);   // saçma kampanya değeri
        Assert.AreEqual(50m, s.Ucret);
        Assert.IsFalse(s.KampanyaEtkili);
    }

    [TestMethod]
    public void Esige_kalan_tutar_dogru()
    {
        Assert.AreEqual(200m, KargoUcretiKurali.EsigeKalan(59.99m, 1000m, 800m, null));
        Assert.IsNull(KargoUcretiKurali.EsigeKalan(59.99m, 1000m, 1000m, null)); // eşik doldu
        Assert.IsNull(KargoUcretiKurali.EsigeKalan(59.99m, 0m, 100m, null));     // eşik yok
        Assert.IsNull(KargoUcretiKurali.EsigeKalan(0m, 1000m, 100m, null));      // kargo zaten ücretsiz
        Assert.IsNull(KargoUcretiKurali.EsigeKalan(59.99m, 1000m, 100m, 0m));    // kampanya bedava yaptı
    }
}
