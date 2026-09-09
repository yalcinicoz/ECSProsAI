using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Tests;

/// <summary>
/// Y1 (2026-09-09): efektif yetki TEK kuralı — <c>(gruplar ∪ kullanıcı-ver) − kullanıcı-kaldır</c>.
/// Tasarım §D çakışma tablosunun her satırı burada sabitlenir. Kanal kapsamı K1 kararı,
/// kullanıcı istisnasının üstünlüğü Ek-1 kararıdır.
/// </summary>
[TestClass]
public sealed class EfektifYetkiHesabiTests
{
    const string Kanalli = "orders.cancel";
    const string Kapsamsiz = "definition.manage";

    static readonly Guid Gulseli = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Julude = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly Guid OlurButik = Guid.Parse("33333333-3333-3333-3333-333333333333");

    static YetkiKaynagi Grup(string key, params Guid[] kanallar)
        => new(key, kanallar.Length > 0, kanallar.Length > 0 ? kanallar : null, YetkiKaynakTipi.Grup);
    static YetkiKaynagi Ver(string key, params Guid[] kanallar)
        => new(key, kanallar.Length > 0, kanallar.Length > 0 ? kanallar : null, YetkiKaynakTipi.KullaniciVer);
    static YetkiKaynagi Kaldir(string key, params Guid[] kanallar)
        => new(key, kanallar.Length > 0, kanallar.Length > 0 ? kanallar : null, YetkiKaynakTipi.KullaniciKaldir);

    [TestMethod]
    public void D1_iki_gruptan_gelen_kanallar_birlesir()
    {
        var e = EfektifYetkiHesabi.Hesapla(false, [Grup(Kanalli, Gulseli), Grup(Kanalli, Julude)]);
        CollectionAssert.AreEquivalent(new[] { Gulseli, Julude }, e.Kanallar(Kanalli)!.ToArray());
        Assert.IsTrue(e.Var(Kanalli, Julude));
    }

    [TestMethod]
    public void D2_bir_grup_vermiyorsa_yasak_degildir()
    {
        // Grup B bu yetkiyi hiç vermiyor (kaynak listesinde yok) → veren grup kazanır.
        var e = EfektifYetkiHesabi.Hesapla(false, [Grup(Kanalli, Gulseli)]);
        Assert.IsTrue(e.Var(Kanalli, Gulseli));
    }

    [TestMethod]
    public void D3_kullaniciya_ozel_verme_eklenir()
    {
        var e = EfektifYetkiHesabi.Hesapla(false, [Grup(Kanalli, Gulseli), Ver(Kanalli, OlurButik)]);
        CollectionAssert.AreEquivalent(new[] { Gulseli, OlurButik }, e.Kanallar(Kanalli)!.ToArray());
    }

    [TestMethod]
    public void D4_kullanici_istisnasi_kanal_bazinda_dusurur()
    {
        // Kararlar belgesindeki örnek: gruptan G+J+O, kullanıcıda Julude kaldırılmış → G+O
        var e = EfektifYetkiHesabi.Hesapla(false,
            [Grup(Kanalli, Gulseli, Julude, OlurButik), Kaldir(Kanalli, Julude)]);
        CollectionAssert.AreEquivalent(new[] { Gulseli, OlurButik }, e.Kanallar(Kanalli)!.ToArray());
        Assert.IsFalse(e.Var(Kanalli, Julude));
        Assert.IsTrue(e.Var(Kanalli));   // hâlâ iki kanalda var
    }

    [TestMethod]
    public void Kaldirma_tum_kanallari_kapatirsa_yetki_biter()
    {
        var e = EfektifYetkiHesabi.Hesapla(false, [Grup(Kanalli, Gulseli), Kaldir(Kanalli, Gulseli)]);
        Assert.IsFalse(e.Var(Kanalli));
        Assert.IsFalse(e.Var(Kanalli, Gulseli));
    }

    [TestMethod]
    public void Kaldirma_kullaniciya_ozel_vermeyi_de_yener()
    {
        // Ek-1: kullanıcı kararı en güçlüdür; hesapta kaldırma EN SONDA uygulanır.
        var e = EfektifYetkiHesabi.Hesapla(false, [Ver(Kanalli, Gulseli), Kaldir(Kanalli, Gulseli)]);
        Assert.IsFalse(e.Var(Kanalli));
    }

    [TestMethod]
    public void Kapsamsiz_yetkide_kanal_sorulmaz()
    {
        var e = EfektifYetkiHesabi.Hesapla(false, [Grup(Kapsamsiz)]);
        Assert.IsTrue(e.Var(Kapsamsiz));
        Assert.IsTrue(e.Var(Kapsamsiz, Julude), "Kapsamsız yetkide kanal yok sayılır.");
        Assert.IsNull(e.Kanallar(Kapsamsiz));
    }

    [TestMethod]
    public void Kapsamsiz_yetki_kullanicida_kaldirilabilir()
    {
        var e = EfektifYetkiHesabi.Hesapla(false, [Grup(Kapsamsiz), Kaldir(Kapsamsiz)]);
        Assert.IsFalse(e.Var(Kapsamsiz));
    }

    [TestMethod]
    public void Kanal_kapsamli_yetki_kanalsiz_verilirse_gecersiz()
    {
        // Kanal kapsamlı yetkiye boş kanal kümesiyle verme = erişim yok (default deny).
        var e = EfektifYetkiHesabi.Hesapla(false,
            [new YetkiKaynagi(Kanalli, true, Array.Empty<Guid>(), YetkiKaynakTipi.Grup)]);
        Assert.IsFalse(e.Var(Kanalli));
    }

    [TestMethod]
    public void D10_super_admin_hesaba_girmez()
    {
        var e = EfektifYetkiHesabi.Hesapla(true, []);
        Assert.IsTrue(e.SuperAdmin);
        Assert.IsTrue(e.Var("hicbir.yerde.olmayan.yetki"));
        Assert.IsTrue(e.Var(Kanalli, Julude));
    }

    [TestMethod]
    public void Hicbir_kaynak_yoksa_yetki_yok()
    {
        var e = EfektifYetkiHesabi.Hesapla(false, []);
        Assert.IsFalse(e.Var(Kanalli));
        Assert.AreEqual(0, e.Keyler.Count);
    }

    [TestMethod]
    public void Bos_kanal_kumesiyle_kaldirma_HICBIR_SEYI_dusurmez()
    {
        // Y4'te yakalanan gerçek hata: panelden "bu kullanıcıda kapat" isteği kanal listesi
        // olmadan gelince kaldırma kaydı BOŞ kümeyle yazılıyor ve hiçbir kanalı düşürmüyordu.
        // Kural burada nettir; "tümünde kaldır" niyetini kanal listesine ÇEVİRMEK çağıranın işidir
        // (API katmanı kanal verilmediğinde o anki tüm kanalları yazar — Ek-2 anlık liste).
        var e = EfektifYetkiHesabi.Hesapla(false,
        [
            Grup(Kanalli, Gulseli, Julude),
            new YetkiKaynagi(Kanalli, true, Array.Empty<Guid>(), YetkiKaynakTipi.KullaniciKaldir),
        ]);
        Assert.IsTrue(e.Var(Kanalli, Gulseli), "Boş kaldırma kümesi hiçbir kanalı düşürmemeli.");
        Assert.AreEqual(2, e.Kanallar(Kanalli)!.Count);

        // Tüm kanallar yazıldığında yetki tamamen kapanır.
        var e2 = EfektifYetkiHesabi.Hesapla(false,
        [
            Grup(Kanalli, Gulseli, Julude),
            Kaldir(Kanalli, Gulseli, Julude),
        ]);
        Assert.IsFalse(e2.Var(Kanalli));
    }
}

