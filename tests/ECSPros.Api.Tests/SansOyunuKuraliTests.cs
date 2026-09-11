using ECSPros.Promotion.Application.Games;
using ECSPros.Promotion.Domain.Entities;

namespace ECSPros.Api.Tests;

/// <summary>Şans oyunları tek kural (docs/BACKEND_OYUNLAR.md §3a): dönem anahtarı, durum/etiket, ağırlıklı seçim, kazı kazan hücre kurgusu, doğrulama.</summary>
[TestClass]
public sealed class SansOyunuKuraliTests
{
    private static Game Oyun(string tip = GameTypes.Wheel, bool alwaysWin = false, string donem = GameLimitPeriods.Day, int hak = 1) => new()
    {
        Code = "cark", Type = tip, TitleI18n = new() { ["tr"] = "Çark" }, AlwaysWin = alwaysWin, StartsAt = DateTime.UtcNow.AddDays(-1),
        LimitPeriod = donem, LimitCount = hak, IsActive = true,
    };
    private static GamePrize Kupon(string ad, int agirlik = 1) => new() { Id = Guid.NewGuid(), Label = ad, Kind = GamePrizeKinds.Coupon, CouponType = "fixed", CouponValue = 50, Weight = agirlik };
    private static GamePrize Pas(int agirlik = 1) => new() { Id = Guid.NewGuid(), Label = "Pas", Kind = GamePrizeKinds.None, Weight = agirlik };

    [TestMethod]
    public void Donem_anahtari_istanbul_gunu_ve_iso_haftasi()
    {
        var utc = new DateTime(2026, 9, 11, 22, 30, 0, DateTimeKind.Utc); // İstanbul 12 Eylül 01:30
        Assert.AreEqual("2026-09-12", SansOyunuKurali.DonemAnahtari(utc, GameLimitPeriods.Day));
        Assert.AreEqual("2026-W37", SansOyunuKurali.DonemAnahtari(utc, GameLimitPeriods.Week));
        Assert.AreEqual("total", SansOyunuKurali.DonemAnahtari(utc, GameLimitPeriods.Total));
        var sonrakiGun = SansOyunuKurali.DonemSonu(utc, GameLimitPeriods.Day)!.Value;
        Assert.AreEqual(new DateTime(2026, 9, 12, 21, 0, 0, DateTimeKind.Utc), sonrakiGun); // 13 Eylül 00:00 İstanbul
        Assert.IsNull(SansOyunuKurali.DonemSonu(utc, GameLimitPeriods.Total));
    }

    [TestMethod]
    public void Durum_hesabi_available_cooldown_exhausted_login_ended()
    {
        var g = Oyun(hak: 2);
        var now = DateTime.UtcNow;
        var d0 = SansOyunuKurali.DurumHesapla(g, now, true, 0);
        Assert.AreEqual(SansOyunuKurali.DurumAvailable, d0.Status); Assert.AreEqual(2, d0.RemainingPlays); Assert.AreEqual("Bugün 2 hakkın var", d0.Label);
        var d2 = SansOyunuKurali.DurumHesapla(g, now, true, 2);
        Assert.AreEqual(SansOyunuKurali.DurumCooldown, d2.Status); Assert.IsNotNull(d2.NextPlayAt); Assert.AreEqual("Yarın tekrar gel", d2.Label);
        Assert.AreEqual(SansOyunuKurali.DurumLoginRequired, SansOyunuKurali.DurumHesapla(g, now, false, 0).Status);
        var toplam = Oyun(donem: GameLimitPeriods.Total, hak: 1);
        Assert.AreEqual(SansOyunuKurali.DurumExhausted, SansOyunuKurali.DurumHesapla(toplam, now, true, 1).Status);
        g.EndsAt = now.AddMinutes(-1);
        Assert.AreEqual(SansOyunuKurali.DurumEnded, SansOyunuKurali.DurumHesapla(g, now, true, 0).Status);
    }

    [TestMethod]
    public void Herkes_kazanir_modunda_pas_asla_secilmez_ve_agirlik_sifir_cikmaz()
    {
        var oduller = new List<GamePrize> { Kupon("A", 1), Pas(100), Kupon("B", 0) };
        var rng = new Random(42);
        for (var i = 0; i < 500; i++)
        {
            var s = SansOyunuKurali.OdulSec(oduller, alwaysWin: true, rng)!;
            Assert.AreEqual("A", s.Label);
        }
        var pasSayisi = Enumerable.Range(0, 500).Count(_ => SansOyunuKurali.OdulSec(oduller, false, rng)!.Kind == GamePrizeKinds.None);
        Assert.IsTrue(pasSayisi > 400, $"ağırlık 100'e karşı 1: pas çoğunlukta olmalı ({pasSayisi})");
    }

    [TestMethod]
    public void Kazi_kazan_kazanan_kurguda_tam_3_ayni_kaybedende_hicbiri_3_kez()
    {
        var oduller = new List<GamePrize> { Kupon("A"), Kupon("B"), Kupon("C"), Kupon("D") };
        var rng = new Random(7);
        for (var i = 0; i < 200; i++)
        {
            var kazanan = oduller[i % 4];
            var hucreler = SansOyunuKurali.KaziKazanHucreleri(oduller, kazanan, rng);
            Assert.AreEqual(6, hucreler.Count);
            var gruplar = hucreler.GroupBy(h => h.PrizeId).ToDictionary(x => x.Key, x => x.Count());
            Assert.AreEqual(3, gruplar[kazanan.Id], "kazanan tam 3 hücre");
            Assert.IsTrue(gruplar.Where(x => x.Key != kazanan.Id).All(x => x.Value < 3), "başka değer 3 kez geçmez");

            var kaybeden = SansOyunuKurali.KaziKazanHucreleri(oduller, null, rng);
            Assert.AreEqual(6, kaybeden.Count);
            Assert.IsTrue(kaybeden.GroupBy(h => h.PrizeId).All(x => x.Count() <= 2), "kaybedende her değer en fazla 2");
        }
    }

    [TestMethod]
    public void Dogrulama_kurallari()
    {
        Assert.IsNull(SansOyunuKurali.Dogrula(Oyun(), new List<GamePrize> { Kupon("A"), Pas() }));
        Assert.IsNotNull(SansOyunuKurali.Dogrula(Oyun(alwaysWin: true), new List<GamePrize> { Kupon("A"), Pas() }), "alwaysWin + pas → hata");
        Assert.IsNotNull(SansOyunuKurali.Dogrula(Oyun(GameTypes.Scratch), new List<GamePrize> { Kupon("A"), Kupon("B") }), "kazı kazan en az 3 ödül");
        Assert.IsNull(SansOyunuKurali.Dogrula(Oyun(GameTypes.Scratch), new List<GamePrize> { Kupon("A"), Kupon("B"), Kupon("C") }));
        Assert.IsNotNull(SansOyunuKurali.Dogrula(Oyun(), new List<GamePrize> { Pas() }), "kazandıran ödül yok");
        var yuzde = new GamePrize { Label = "X", Kind = GamePrizeKinds.Coupon, CouponType = "percentage", CouponValue = 120, Weight = 1 };
        Assert.IsNotNull(SansOyunuKurali.Dogrula(Oyun(), new List<GamePrize> { yuzde }), "yüzde 100'ü aşamaz");
        var kotuKod = Oyun(); kotuKod.Code = "Çark Oyunu";
        Assert.IsNotNull(SansOyunuKurali.Dogrula(kotuKod, new List<GamePrize> { Kupon("A") }));
    }
}
