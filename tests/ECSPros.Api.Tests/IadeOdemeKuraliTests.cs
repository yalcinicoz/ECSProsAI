using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Contracts;

namespace ECSPros.Api.Tests;

/// <summary>
/// İade akışı planı (2026-09-10, kullanıcı kararları R1-R10): para iadesi yalnız tahsilat varsa; kapıda ödeme
/// teslimsiz → asla; pazaryeri → asla; Teslimatsız İade yalnız kargoda / faturalı-kargosuz siparişte; `processing`
/// artık iptal edilebilir (fatura ön koşulu handler'da). F5 kabul testinin kural + domain katmanı; handler
/// senaryoları (fatura/gönderi ön koşulları) elle test talimatında.
/// </summary>
[TestClass]
public sealed class IadeOdemeKuraliTests
{
    private static IadeOdemeKurali.Girdi G(bool pazaryeri = false, decimal tahsil = 0m, decimal onceki = 0m,
        string? yontem = "kart", bool teslim = false) => new(pazaryeri, tahsil, onceki, yontem, teslim);

    [TestMethod]
    public void Pazaryeri_siparisinde_hicbir_durumda_para_iadesi_yok()   // R10
    {
        var s = IadeOdemeKurali.Degerlendir(G(pazaryeri: true, tahsil: 500m, teslim: true));
        Assert.IsFalse(s.Uygun);
        Assert.AreEqual(IadeOdemeKurali.NedenPazaryeri, s.Neden);
        Assert.AreEqual(0m, s.UstSinir);
    }

    [TestMethod]
    public void Kapida_odeme_teslimsiz_iadede_tahsilat_yok_para_iadesi_hesaplanmaz()   // R8
    {
        var s = IadeOdemeKurali.Degerlendir(G(tahsil: 0m, yontem: "kapida-nakit", teslim: false));
        Assert.IsFalse(s.Uygun);
        Assert.AreEqual(IadeOdemeKurali.NedenKapidaTahsilatYok, s.Neden);
    }

    [TestMethod]
    public void Kapida_odeme_teslim_edilip_tahsilat_kaydi_atildiysa_uygun()   // R9
    {
        var s = IadeOdemeKurali.Degerlendir(G(tahsil: 350m, yontem: "kapida-kart", teslim: true));
        Assert.IsTrue(s.Uygun);
        Assert.AreEqual(350m, s.UstSinir);
    }

    [TestMethod]
    public void Kart_odemesinde_tahsilat_var_her_iade_tipinde_geri_odeme_var()   // R7
    {
        var s = IadeOdemeKurali.Degerlendir(G(tahsil: 1200m, yontem: "kart", teslim: false));
        Assert.IsTrue(s.Uygun);
        Assert.AreEqual(1200m, s.UstSinir);
    }

    [TestMethod]
    public void Ust_sinir_daha_once_iade_edilen_dusulmus_kalandir()
    {
        var s = IadeOdemeKurali.Degerlendir(G(tahsil: 1000m, onceki: 400m));
        Assert.IsTrue(s.Uygun);
        Assert.AreEqual(600m, s.UstSinir);

        var tamami = IadeOdemeKurali.Degerlendir(G(tahsil: 1000m, onceki: 1000m));
        Assert.IsFalse(tamami.Uygun);
        Assert.AreEqual(IadeOdemeKurali.NedenZatenIadeEdildi, tamami.Neden);
    }

    [TestMethod]
    public void Tahsilat_yoksa_kart_siparisinde_neden_unpaid()
    {
        var s = IadeOdemeKurali.Degerlendir(G(tahsil: 0m, yontem: "kart"));
        Assert.IsFalse(s.Uygun);
        Assert.AreEqual(IadeOdemeKurali.NedenTahsilatYok, s.Neden);
    }

    [TestMethod]
    public void Tahsil_edilen_odeme_satiri_yoksa_paid_durumu_siparis_toplamina_duser()
    {
        // Plandan önceki kart siparişleri: ödeme satırı yok, PaymentStatus=paid → GrandTotal tahsil edilmiş sayılır.
        Assert.AreEqual(899.90m, IadeOdemeKurali.TahsilEdilen(0m, "paid", 899.90m));
        Assert.AreEqual(0m, IadeOdemeKurali.TahsilEdilen(0m, "unpaid", 899.90m));
        // Ödeme satırı varsa o kazanır (eksik ödemede gerçek tutar).
        Assert.AreEqual(500m, IadeOdemeKurali.TahsilEdilen(500m, "underpaid", 899.90m));
    }

    [TestMethod]
    public void Tutar_ust_sinirla_kirpilir()
    {
        Assert.AreEqual(600m, IadeOdemeKurali.TutarKirp(750m, 600m));
        Assert.AreEqual(300m, IadeOdemeKurali.TutarKirp(300m, 600m));
        Assert.AreEqual(0m, IadeOdemeKurali.TutarKirp(-5m, 600m));
    }

    // ── Domain: sipariş durum makinesi ──

    private static ECSPros.Order.Domain.Entities.Order Siparis(string status)
    {
        var o = new ECSPros.Order.Domain.Entities.Order { Status = status, OrderNumber = "T-1" };
        o.Items.Add(new OrderItem { VariantId = Guid.NewGuid(), Quantity = 2, Total = 100m });
        return o;
    }

    [TestMethod]
    public void Teslimatsiz_iade_kargoda_siparisi_returned_yapar_ve_wasShipped_true()   // R2/R3
    {
        var o = Siparis("shipped");
        o.MarkUndeliveredReturn(Guid.NewGuid(), "Müşteri kabul etmedi");
        Assert.AreEqual("returned", o.Status);
        var ev = o.DomainEvents.OfType<ECSPros.Order.Domain.Events.OrderReturnedUndeliveredEvent>().Single();
        Assert.IsTrue(ev.WasShipped);
        StringAssert.StartsWith(o.InternalNotes, "[Teslimatsız İade] Müşteri kabul etmedi");
    }

    [TestMethod]
    public void Teslimatsiz_iade_islemdeki_siparisi_kabul_eder_wasShipped_false()   // faturalı-kargosuz; fatura kontrolü handler'da
    {
        var o = Siparis("processing");
        o.MarkUndeliveredReturn(Guid.NewGuid(), "Fatura kesildi, gönderilmeyecek");
        Assert.AreEqual("returned", o.Status);
        Assert.IsFalse(o.DomainEvents.OfType<ECSPros.Order.Domain.Events.OrderReturnedUndeliveredEvent>().Single().WasShipped);
    }

    [TestMethod]
    public void Teslimatsiz_iade_onceki_asamalarda_ve_teslimde_olamaz()   // R3 olumsuz
    {
        foreach (var durum in new[] { "pending", "confirmed", "delivered", "cancelled", "returned" })
            Assert.ThrowsExactly<InvalidOperationException>(() => Siparis(durum).MarkUndeliveredReturn(Guid.NewGuid(), "x"), durum);
    }

    [TestMethod]
    public void Teslimatsiz_iade_neden_zorunlu()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => Siparis("shipped").MarkUndeliveredReturn(Guid.NewGuid(), "  "));
    }

    [TestMethod]
    public void Islemdeki_siparis_iptal_edilebilir_kargodaki_edilemez()   // R4 (fatura ön koşulu handler'da)
    {
        var o = Siparis("processing");
        o.Cancel(Guid.NewGuid(), "vazgeçti");
        Assert.AreEqual("cancelled", o.Status);
        Assert.ThrowsExactly<InvalidOperationException>(() => Siparis("shipped").Cancel(Guid.NewGuid()));
    }

    // ── Sözlük ──

    [TestMethod]
    public void Sozluk_returned_panelde_teslimatsiz_iade_vitrinde_teslim_edilemedi()   // R5, K7
    {
        Assert.AreEqual("Teslimatsız İade", DurumEtiketleri.Etiket(DurumEtiketleri.Panel.SiparisDurumu, "returned"));
        Assert.AreEqual("Teslim Edilemedi", DurumEtiketleri.Etiket(DurumEtiketleri.Vitrin.SiparisDurumu, "returned"));
        Assert.AreEqual("Teslimatsız İade", DurumEtiketleri.Etiket(DurumEtiketleri.Panel.IadeTipi, ReturnConstants.TypeUndelivered));
        Assert.IsTrue(DurumEtiketleri.Vitrin.Aileler.ContainsKey("returnType"));
        Assert.IsTrue(DurumEtiketleri.IadeTamamlandi("closed"));
        Assert.IsFalse(DurumEtiketleri.IadeTamamlandi("received"));
        // closed: akış şeridi tamam, "current" yok
        var akis = DurumEtiketleri.IadeAkisi("closed");
        Assert.AreEqual(4, akis.Count);
        Assert.IsTrue(akis.All(a => a.Done));
    }

    [TestMethod]
    public void Varsayilan_geri_odeme_yontemi_odeme_yonteminden()
    {
        Assert.AreEqual(ReturnConstants.RefundMethodCardRefund, ReturnConstants.RefundMethodFor("kart"));
        Assert.AreEqual(ReturnConstants.RefundMethodBankTransfer, ReturnConstants.RefundMethodFor("kapida-nakit"));
        Assert.AreEqual(ReturnConstants.RefundMethodOriginalPayment, ReturnConstants.RefundMethodFor(null));
    }
}
