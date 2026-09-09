using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Shared.Contracts;

namespace ECSPros.Api.Tests;

/// <summary>
/// M4 (2026-09-09, mobil isteği 4): durum kodu → etiket/renk tek kural.
///
/// Korunan kurallar:
///  • Panel ile vitrin etiketleri BİLİNÇLİ farklı (pending/confirmed panelde ayrı, vitrinde tek metin) —
///    bu test farkın kazara silinmesini engeller.
///  • Panel etiketleri orderConstants.ts ile birebir aynı kalmalı (Excel export ve grid aynı metni yazar).
///  • Bilinmeyen kod ASLA boş dönmez: ham kod gösterilir (yeni durum eklenince ekran boşalmasın).
///  • Akış şeridi site Hesabım şeridiyle aynı: iptal edilende BOŞ, teslim edilende hepsi tamam.
///  • Sürüm içerikten türer — etiket değişince ETag değişir, istemci önbelleği tazelenir.
/// </summary>
[TestClass]
public sealed class DurumEtiketleriTests
{
    // ── Panel dili (orderConstants.ts / Excel) ──

    [TestMethod]
    public void Panel_siparis_etiketleri_orderConstants_ile_ayni()
    {
        Assert.AreEqual("Bekleyen", OrderGrid.StatusLabel("pending"));
        Assert.AreEqual("Onaylı", OrderGrid.StatusLabel("confirmed"));
        Assert.AreEqual("İşlemde", OrderGrid.StatusLabel("processing"));
        Assert.AreEqual("Kargoda", OrderGrid.StatusLabel("shipped"));
        Assert.AreEqual("Teslim", OrderGrid.StatusLabel("delivered"));
        Assert.AreEqual("İptal", OrderGrid.StatusLabel("cancelled"));
        Assert.AreEqual("İade", OrderGrid.StatusLabel("returned"));
        Assert.AreEqual("Ödendi", OrderGrid.PaymentStatusLabel("paid"));
        Assert.AreEqual("Kart (Online)", OrderGrid.PaymentMethodLabel("kart"));
    }

    [TestMethod]
    public void Panel_ve_vitrin_dili_ayri_kalir()
    {
        // Personel iç onay adımını görmek zorunda, müşteri görmemeli.
        Assert.AreNotEqual(
            DurumEtiketleri.Etiket(DurumEtiketleri.Panel.SiparisDurumu, "pending"),
            DurumEtiketleri.Etiket(DurumEtiketleri.Panel.SiparisDurumu, "confirmed"));
        Assert.AreEqual(
            DurumEtiketleri.Etiket(DurumEtiketleri.Vitrin.SiparisDurumu, "pending"),
            DurumEtiketleri.Etiket(DurumEtiketleri.Vitrin.SiparisDurumu, "confirmed"));
        Assert.AreEqual("Sipariş Alındı", DurumEtiketleri.Etiket(DurumEtiketleri.Vitrin.SiparisDurumu, "confirmed"));
    }

    // ── Bilinmeyen / boş kod ──

    [TestMethod]
    public void Bilinmeyen_kod_ham_haliyle_gosterilir()
    {
        Assert.AreEqual("on_hold", DurumEtiketleri.Etiket(DurumEtiketleri.Vitrin.SiparisDurumu, "on_hold"));
        Assert.AreEqual(DurumEtiketleri.Neutral, DurumEtiketleri.Varyant(DurumEtiketleri.Vitrin.SiparisDurumu, "on_hold"));
        Assert.AreEqual("", DurumEtiketleri.Etiket(DurumEtiketleri.Vitrin.OdemeYontemi, null));
    }

    // ── Akış şeridi ──

    [TestMethod]
    public void Iptal_edilen_siparisin_akis_seridi_bos()
        => Assert.AreEqual(0, DurumEtiketleri.SiparisAkisi("cancelled").Count);

    [TestMethod]
    public void Kargodaki_siparis_ucuncu_adimda()
    {
        var akis = DurumEtiketleri.SiparisAkisi("shipped");
        Assert.AreEqual(4, akis.Count);
        Assert.IsTrue(akis[0].Done && akis[1].Done);
        Assert.IsFalse(akis[2].Done);
        Assert.IsTrue(akis[2].Current);          // "Kargoda" devam ediyor
        Assert.IsFalse(akis[3].Done || akis[3].Current);
        Assert.AreEqual("kargoda", akis[2].Code);
    }

    [TestMethod]
    public void Teslim_edilen_sipariste_tum_adimlar_tamam_ve_current_yok()
    {
        var akis = DurumEtiketleri.SiparisAkisi("delivered");
        Assert.IsTrue(akis.All(a => a.Done));
        Assert.IsFalse(akis.Any(a => a.Current));
    }

    [TestMethod]
    public void Reddedilen_iadenin_akis_seridi_bos_onaylanan_ikinci_adimda()
    {
        Assert.AreEqual(0, DurumEtiketleri.IadeAkisi("rejected").Count);
        var akis = DurumEtiketleri.IadeAkisi("approved");
        Assert.IsTrue(akis[0].Done);
        Assert.IsTrue(akis[1].Current);
    }

    // ── Aksiyon bayrakları: komutların dayattığı kuralla aynı olmalı ──

    [TestMethod]
    public void Aksiyon_bayraklari_komut_kurallariyla_ortusur()
    {
        // Order.CancellableStatuses = [pending, confirmed]
        Assert.IsTrue(DurumEtiketleri.IptalEdilebilir("pending"));
        Assert.IsTrue(DurumEtiketleri.IptalEdilebilir("confirmed"));
        Assert.IsFalse(DurumEtiketleri.IptalEdilebilir("processing"));
        Assert.IsFalse(DurumEtiketleri.IptalEdilebilir("shipped"));

        // CreateStoreReturnCommandHandler: yalnız teslim edilmiş sipariş iade edilebilir
        Assert.IsTrue(DurumEtiketleri.IadeEdilebilir("delivered"));
        Assert.IsFalse(DurumEtiketleri.IadeEdilebilir("shipped"));
        Assert.IsFalse(DurumEtiketleri.YorumYazilabilir("cancelled"));
    }

    // ── DTO'ya türetilmiş alanlar ──

    [TestMethod]
    public void Siparis_listesi_satiri_vitrin_etiketini_tasir()
    {
        var satir = new OrderListDto(
            Guid.NewGuid(), "MIS0000001", null, "shipped", "paid", 100m, "TRY", DateTime.UtcNow,
            "Ali Veli", "kapida-nakit");
        Assert.AreEqual("Kargoda", satir.StatusLabel);
        Assert.AreEqual("Ödeme Alındı", satir.PaymentStatusLabel);
        Assert.AreEqual("Kapıda Nakit Ödeme", satir.PaymentMethodLabel);
        Assert.IsTrue(satir.StatusColor.StartsWith('#'));
        Assert.IsFalse(satir.CanCancel);   // kargoya verilmiş sipariş müşteri tarafından iptal edilemez
        Assert.IsFalse(satir.CanReturn);   // iade yalnız teslim sonrası
    }

    // ── Sürüm ──

    [TestMethod]
    public void Surum_kararli_ve_icerikten_turemis()
    {
        Assert.AreEqual(12, DurumEtiketleri.Surum.Length);
        Assert.IsTrue(DurumEtiketleri.Surum.All(c => char.IsAsciiHexDigitLower(c)));
    }

    [TestMethod]
    public void Lookups_aileleri_mobil_sozlesmesindeki_anahtarlari_tasir()
    {
        foreach (var anahtar in new[] { "orderStatus", "paymentStatus", "paymentMethod", "returnStatus", "reviewStatus", "questionStatus" })
            Assert.IsTrue(DurumEtiketleri.Vitrin.Aileler.ContainsKey(anahtar), anahtar);
        // Her etiket dolu ve her renk hex olmalı — istemci boş metin/renk almamalı.
        foreach (var (_, aile) in DurumEtiketleri.Vitrin.Aileler)
            foreach (var e in aile)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.Etiket), e.Kod);
                Assert.IsTrue(e.Renk.StartsWith('#') && e.Renk.Length == 7, e.Kod);
            }
    }
}
