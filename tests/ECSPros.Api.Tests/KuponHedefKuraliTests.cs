using ECSPros.Promotion.Application.Services;

namespace ECSPros.Api.Tests;

/// <summary>
/// 2026-09-09: kişiye/gruba özel kupon tanımı. Kural TEK yerde (KuponHedefKurali) — ValidateCoupon
/// ve UseCoupon aynı cevabı verir. Regresyon: kişiye özel kupon MİSAFİR sepetinde (MemberId null)
/// eskiden doğrulamadan geçiyordu.
/// </summary>
[TestClass]
public sealed class KuponHedefKuraliTests
{
    static readonly Guid Uye = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid BaskaUye = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly Guid Grup = Guid.Parse("33333333-3333-3333-3333-333333333333");
    static readonly Guid BaskaGrup = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [TestMethod]
    public void Herkese_acik_kupon_misafirde_de_gecerli()
        => Assert.IsNull(KuponHedefKurali.Engel(null, null, null, null));

    [TestMethod]
    public void Kisiye_ozel_kupon_sahibinde_gecerli()
        => Assert.IsNull(KuponHedefKurali.Engel(Uye, null, Uye, null));

    [TestMethod]
    public void Kisiye_ozel_kupon_baskasinda_reddedilir()
        => Assert.AreEqual(KuponHedefKurali.BaskasinaAit,
            KuponHedefKurali.Engel(Uye, null, BaskaUye, null));

    [TestMethod]
    public void Kisiye_ozel_kupon_misafirde_reddedilir()   // regresyon
        => Assert.AreEqual(KuponHedefKurali.GirisGerekli,
            KuponHedefKurali.Engel(Uye, null, null, null));

    [TestMethod]
    public void Grup_kuponu_grup_uyesinde_gecerli()
        => Assert.IsNull(KuponHedefKurali.Engel(null, Grup, Uye, Grup));

    [TestMethod]
    public void Grup_kuponu_baska_grupta_ve_grupsuz_uyede_reddedilir()
    {
        Assert.AreEqual(KuponHedefKurali.GrupDisi, KuponHedefKurali.Engel(null, Grup, Uye, BaskaGrup));
        Assert.AreEqual(KuponHedefKurali.GrupDisi, KuponHedefKurali.Engel(null, Grup, Uye, null));
    }

    [TestMethod]
    public void Grup_kuponu_misafirde_reddedilir()
        => Assert.AreEqual(KuponHedefKurali.GirisGerekli,
            KuponHedefKurali.Engel(null, Grup, null, null));

    [TestMethod]
    public void Tanimda_uye_ve_grup_birlikte_secilemez()
    {
        Assert.AreEqual(KuponHedefKurali.IkisiBirden, KuponHedefKurali.TanimHatasi(Uye, Grup));
        Assert.IsNull(KuponHedefKurali.TanimHatasi(Uye, null));
        Assert.IsNull(KuponHedefKurali.TanimHatasi(null, Grup));
        Assert.IsNull(KuponHedefKurali.TanimHatasi(null, null));
    }

    [TestMethod]
    public void Uye_grubu_yalniz_grup_hedefli_kuponda_sorgulanir()
    {
        Assert.IsFalse(KuponHedefKurali.UyeGrubuGerekli(null, Uye));   // herkese açık → CRM sorgusu yok
        Assert.IsFalse(KuponHedefKurali.UyeGrubuGerekli(Grup, null));  // misafir → sorgu yok
        Assert.IsTrue(KuponHedefKurali.UyeGrubuGerekli(Grup, Uye));
    }
}
