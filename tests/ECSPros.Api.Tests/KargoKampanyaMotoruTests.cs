using ECSPros.Promotion.Application.Services.Engine;
using ECSPros.Promotion.Domain.Entities;

namespace ECSPros.Api.Tests;

/// <summary>
/// 2026-09-09: definition kataloğunda <c>free_shipping</c> ("Kargo Kampanyası") tipi ve şeması
/// vardı ama <see cref="CampaignEngine"/> switch'inde karşılığı YOKTU — panelde tanımlanan kargo
/// kampanyaları sessizce çalışmıyordu (2026-08-27'de discount/bundle için görülen hatanın aynısı).
/// Şema alanları: thresholdType(none|cartAmount) + thresholdValue, paymentMethods(all|credit_card),
/// coverage(full|percent|amount) + coverageValue.
/// </summary>
[TestClass]
public sealed class KargoKampanyaMotoruTests
{
    private const decimal KanalUcret = 60m;

    private static Campaign Kampanya(params (string Key, object Value)[] ayarlar)
    {
        var c = new Campaign
        {
            Code = "KARGO",
            NameI18n = new Dictionary<string, string> { ["tr"] = "Kargo Bedava" },
            CampaignType = new CampaignType { Code = "free_shipping", Scope = "shipping" },
        };
        foreach (var (k, v) in ayarlar) c.Settings[k] = v;
        return c;
    }

    [TestMethod]
    public void Kosulsuz_kampanya_kargoyu_bedava_yapar()
        => Assert.AreEqual(0m, CampaignEngine.KargoUcreti(
            Kampanya(("thresholdType", "none"), ("coverage", "full")), 59.99m, null, KanalUcret));

    [TestMethod]
    public void Sepet_esigi_altinda_kampanya_uygulanmaz()
        => Assert.IsNull(CampaignEngine.KargoUcreti(
            Kampanya(("thresholdType", "cartAmount"), ("thresholdValue", 750m), ("coverage", "full")),
            500m, null, KanalUcret));

    [TestMethod]
    public void Sepet_esigi_ustunde_kampanya_uygulanir()
        => Assert.AreEqual(0m, CampaignEngine.KargoUcreti(
            Kampanya(("thresholdType", "cartAmount"), ("thresholdValue", 750m), ("coverage", "full")),
            800m, null, KanalUcret));

    [TestMethod]
    public void Yuzde_kapsami_ucreti_indirir()
        => Assert.AreEqual(30m, CampaignEngine.KargoUcreti(
            Kampanya(("coverage", "percent"), ("coverageValue", 50m)), 100m, null, KanalUcret));

    [TestMethod]
    public void Tutar_kapsami_ucretten_duser_ve_sifirin_altina_inmez()
    {
        Assert.AreEqual(40m, CampaignEngine.KargoUcreti(
            Kampanya(("coverage", "amount"), ("coverageValue", 20m)), 100m, null, KanalUcret));
        Assert.AreEqual(0m, CampaignEngine.KargoUcreti(
            Kampanya(("coverage", "amount"), ("coverageValue", 500m)), 100m, null, KanalUcret));
    }

    [TestMethod]
    public void Yontem_kisitli_kampanya_yontem_secilmeden_uygulanmaz()
    {
        var k = Kampanya(("paymentMethods", "credit_card"), ("coverage", "full"));
        Assert.IsNull(CampaignEngine.KargoUcreti(k, 100m, null, KanalUcret));           // sepet adımı
        Assert.IsNull(CampaignEngine.KargoUcreti(k, 100m, "kapida-nakit", KanalUcret)); // kapıda nakit
        Assert.AreEqual(0m, CampaignEngine.KargoUcreti(k, 100m, "kart", KanalUcret));   // kart
        Assert.AreEqual(0m, CampaignEngine.KargoUcreti(k, 100m, "kapida-kart", KanalUcret));
    }

    [TestMethod]
    public void Kargo_kampanyasi_urun_indirimi_uretmez()
    {
        // Calculate (ürün/sepet indirimi) free_shipping tipinde null döner — kargo ayrı yoldan işler.
        var line = CampaignEngine.Calculate(
            Kampanya(("coverage", "full")),
            new List<CartLineItem> { new(Guid.NewGuid(), 1, 100m) },
            new HashSet<Guid>());
        Assert.IsNull(line);
    }
}
