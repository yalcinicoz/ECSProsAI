using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Tests;

/// <summary>
/// Y6 (2026-09-09, karar K6): hassas alan izinleri — v1'de beş alan.
/// Kural: yetkisi olmayana DEĞER GİTMEZ; metin alanı maskelenir, sayısal alan null döner.
/// Varsayılan kurucu HER ŞEYİ KAPALI üretir (default deny) — izin geçirmeyi unutan çağıran
/// veri sızdırmaz, maskeler.
/// </summary>
[TestClass]
public sealed class AlanIzinleriTests
{
    [TestMethod]
    public void Varsayilan_hicbir_hassas_alani_gostermez()
    {
        var izin = new AlanIzinleri();
        Assert.AreEqual(AlanIzinleri.Gizli, izin.Telefonla("05551112233"));
        Assert.AreEqual(AlanIzinleri.Gizli, izin.Adresle("Test mah. 1"));
        Assert.AreEqual(AlanIzinleri.Gizli, izin.Notla("iç not"));
        Assert.IsNull(izin.Maliyetle(120m));
        Assert.IsNull(izin.Karla(30m));
    }

    [TestMethod]
    public void Yetki_varsa_deger_aynen_doner()
    {
        var izin = AlanIzinleri.Tam;
        Assert.AreEqual("05551112233", izin.Telefonla("05551112233"));
        Assert.AreEqual("Test mah. 1", izin.Adresle("Test mah. 1"));
        Assert.AreEqual("iç not", izin.Notla("iç not"));
        Assert.AreEqual(120m, izin.Maliyetle(120m));
        Assert.AreEqual(30m, izin.Karla(30m));
    }

    [TestMethod]
    public void Bos_deger_maskelenmez_bos_kalir()
    {
        // "boş" ile "gizli" karışmamalı: zaten değeri olmayan alan maske göstermez.
        var izin = AlanIzinleri.Yok;
        Assert.IsNull(izin.Telefonla(null));
        Assert.AreEqual("", izin.Adresle(""));
    }

    [TestMethod]
    public void Alanlar_bagimsizdir()
    {
        var yalnizTelefon = new AlanIzinleri(Telefon: true);
        Assert.AreEqual("0555", yalnizTelefon.Telefonla("0555"));
        Assert.AreEqual(AlanIzinleri.Gizli, yalnizTelefon.Adresle("adres"));
        Assert.IsNull(yalnizTelefon.Maliyetle(10m));
    }

    [TestMethod]
    public void Export_kolonu_alan_yetkisine_gore_suzulur()
    {
        var izin = new AlanIzinleri(Maliyet: true);
        Assert.IsTrue(izin.KolonGorunur(null), "Etiketsiz kolon herkese görünür.");
        Assert.IsTrue(izin.KolonGorunur("cost"));
        Assert.IsFalse(izin.KolonGorunur("phone"));
        Assert.IsFalse(izin.KolonGorunur("address"));
        Assert.IsFalse(izin.KolonGorunur("notes"));
        Assert.IsTrue(AlanIzinleri.Tam.KolonGorunur("phone"));
    }
}
