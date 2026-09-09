using ECSPros.Iam.Application.Yetkilendirme;

namespace ECSPros.Api.Tests;

/// <summary>
/// Y8 (2026-09-09, §J.1): yetkisiz erişim denemesi ÖRNEKLENEREK yazılır.
/// Bu testler iki uçtan da korur: örnekleme çalışmazsa tablo şişer, fazla çalışırsa sinyal kaybolur.
/// </summary>
[TestClass]
public sealed class YetkisizErisimOrneklemeTests
{
    [TestMethod]
    public void Ilk_deneme_HER_ZAMAN_yazilir()
    {
        var o = new YetkisizErisimOrnekleyici();
        Assert.IsTrue(o.YazilsinMi("kullanici|yetki|orders.manage|GET /api/orders", out var atlanan));
        Assert.AreEqual(0, atlanan, "İlk kayıtta atlanan deneme olmamalı.");
    }

    [TestMethod]
    public void Ayni_anahtarin_tekrari_pencere_icinde_yazilmaz_ama_SAYILIR()
    {
        var o = new YetkisizErisimOrnekleyici();
        const string a = "u1|yetki|orders.manage|POST /api/orders/x/cancel";

        Assert.IsTrue(o.YazilsinMi(a, out _));
        for (var i = 0; i < 50; i++)
            Assert.IsFalse(o.YazilsinMi(a, out _), "Pencere içindeki tekrar yazılmamalı.");
    }

    [TestMethod]
    public void Farkli_kullanici_ve_farkli_uc_ayri_izlenir()
    {
        var o = new YetkisizErisimOrnekleyici();
        Assert.IsTrue(o.YazilsinMi("u1|yetki|orders.manage|GET /a", out _));
        Assert.IsTrue(o.YazilsinMi("u2|yetki|orders.manage|GET /a", out _), "Başka kullanıcı ayrı kayıttır.");
        Assert.IsTrue(o.YazilsinMi("u1|yetki|orders.manage|GET /b", out _), "Başka uç ayrı kayıttır.");
        Assert.IsTrue(o.YazilsinMi("u1|kanal|orders.view|GET /a", out _), "Başka ret türü ayrı kayıttır.");
    }

    [TestMethod]
    public void Pencere_dolunca_yeniden_yazilir_ve_atlananlar_raporlanir()
    {
        var o = new YetkisizErisimOrnekleyici();
        const string a = "u1|yetki|orders.manage|GET /api/orders";

        Assert.IsTrue(o.YazilsinMi(a, out _));
        for (var i = 0; i < 7; i++) o.YazilsinMi(a, out _);

        // Pencerenin dolmasını beklemek yerine son yazım zamanını geriye alıyoruz:
        // testin 10 dakika sürmesi kabul edilemez, kural aynı kalır.
        GeriAl(o, a, YetkisizErisimOrnekleyici.Pencere + TimeSpan.FromSeconds(1));

        Assert.IsTrue(o.YazilsinMi(a, out var atlanan));
        Assert.AreEqual(7, atlanan, "Penceredeki atlanan denemeler bir sonraki kayda taşınmalı.");

        // Sayaç sıfırlanır: aynı olay iki kez raporlanmaz.
        GeriAl(o, a, YetkisizErisimOrnekleyici.Pencere + TimeSpan.FromSeconds(1));
        Assert.IsTrue(o.YazilsinMi(a, out var ikinci));
        Assert.AreEqual(0, ikinci);
    }

    [TestMethod]
    public void Bellek_sinirlidir_binlerce_farkli_anahtar_sozlugu_sisirmez()
    {
        var o = new YetkisizErisimOrnekleyici();
        // Saldırgan yolu değiştirerek anahtar üretebilir; sözlük sınırsız büyümemeli.
        for (var i = 0; i < 20_000; i++)
            Assert.IsTrue(o.YazilsinMi($"u1|yetki|x|GET /api/{i}", out _), "Sınır dolunca da kayıt YAZILMALI.");

        Assert.IsTrue(SozlukSayisi(o) <= 5000, $"Sözlük sınırı aşıldı: {SozlukSayisi(o)}");
    }

    // ── test yardımcıları (özel alanlara yalnız test erişir) ──────────────────
    static object SayacNesnesi(YetkisizErisimOrnekleyici o, string anahtar)
    {
        var alan = typeof(YetkisizErisimOrnekleyici)
            .GetField("_sayaclar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var sozluk = alan.GetValue(o)!;
        var indeksleyici = sozluk.GetType().GetProperty("Item")!;
        return indeksleyici.GetValue(sozluk, [anahtar])!;
    }

    static void GeriAl(YetkisizErisimOrnekleyici o, string anahtar, TimeSpan sure)
    {
        var sayac = SayacNesnesi(o, anahtar);
        var alan = sayac.GetType().GetField("SonYazim")!;
        alan.SetValue(sayac, (DateTime)alan.GetValue(sayac)! - sure);
    }

    static int SozlukSayisi(YetkisizErisimOrnekleyici o)
    {
        var alan = typeof(YetkisizErisimOrnekleyici)
            .GetField("_sayaclar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var sozluk = alan.GetValue(o)!;
        return (int)sozluk.GetType().GetProperty("Count")!.GetValue(sozluk)!;
    }
}
