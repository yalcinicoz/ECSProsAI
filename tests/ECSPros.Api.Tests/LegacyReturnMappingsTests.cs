using ECSPros.Api.Services.LegacyImport;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Contracts;

namespace ECSPros.Api.Tests;

/// <summary>
/// İade planı legacy düzeltmesi (2026-09-10): eski iade verisi yeni sözlüğe eşlenir — iadeTipi 1/2 =
/// undelivered/customer (eski TeslimatsizIade/SiparisUrunIade kodundan doğrulandı), üyeye ödeme webuyeparalari'ndan,
/// geri ödeme uygunluğu IadeOdemeKurali ile.
/// </summary>
[TestClass]
public sealed class LegacyReturnMappingsTests
{
    [TestMethod]
    public void BilinenNedenleriSabitKodlaraEsler()
    {
        Assert.AreEqual("legacy_unspecified", LegacyReturnMappings.ReasonCode(1));
        Assert.AreEqual("legacy_disliked", LegacyReturnMappings.ReasonCode(2));
        Assert.AreEqual("legacy_size", LegacyReturnMappings.ReasonCode(3));
        Assert.AreEqual("legacy_defective", LegacyReturnMappings.ReasonCode(4));
        Assert.AreEqual("legacy_low_quality", LegacyReturnMappings.ReasonCode(5));
        Assert.AreEqual("legacy_not_delivered", LegacyReturnMappings.ReasonCode(9));
        Assert.AreEqual("legacy_unknown", LegacyReturnMappings.ReasonCode(999));
    }

    [TestMethod]
    public void IadeTipi_EskiKoddan_Dogrulanan_Sozluge_Esler()
    {
        Assert.AreEqual(ReturnConstants.TypeUndelivered, LegacyReturnMappings.ReturnType(1));
        Assert.AreEqual(ReturnConstants.TypeCustomer, LegacyReturnMappings.ReturnType(2));
        Assert.AreEqual("legacy_type_7", LegacyReturnMappings.ReturnType(7));   // bilinmeyen tahmin edilmez
    }

    [TestMethod]
    public void GeriOdemeYontemi_DegisimYalnizsaCuzdan_DegilseOdemeYontemine()
    {
        Assert.AreEqual(ReturnConstants.RefundMethodWallet, LegacyReturnMappings.RefundMethod("kart", exchangeOnly: true));
        Assert.AreEqual(ReturnConstants.RefundMethodCardRefund, LegacyReturnMappings.RefundMethod("kart", exchangeOnly: false));
        Assert.AreEqual(ReturnConstants.RefundMethodBankTransfer, LegacyReturnMappings.RefundMethod("kapida-nakit", exchangeOnly: false));
    }

    [TestMethod]
    public void UyeyeOdendiyse_Refunded_Completed()
    {
        var kural = IadeOdemeKurali.Degerlendir(new(false, 500m, 0m, "kart", true));
        var h = LegacyReturnMappings.Durum(legacyPaidToMember: 350m, legacyCreditToMember: 350m, legacyAmount: 350m, kural);
        Assert.AreEqual(ReturnConstants.StatusRefunded, h.Status);
        Assert.AreEqual(ReturnConstants.RefundCompleted, h.RefundStatus);
        Assert.AreEqual(350m, h.RefundAmount);
    }

    [TestMethod]
    public void AlacakAcilmisAmaOdenmemisse_Received_Pending()
    {
        var kural = IadeOdemeKurali.Degerlendir(new(false, 500m, 0m, "kart", true));
        var h = LegacyReturnMappings.Durum(0m, 199.99m, 199.99m, kural);
        Assert.AreEqual(ReturnConstants.StatusReceived, h.Status);
        Assert.AreEqual(ReturnConstants.RefundPending, h.RefundStatus);
        Assert.AreEqual(199.99m, h.RefundAmount);
    }

    [TestMethod]
    public void KapidaOdemeTeslimsiz_AlacakYok_Closed_NotApplicable()   // R8 — eski sistemde de üyeye ödeme satırı açılmaz
    {
        var kural = IadeOdemeKurali.Degerlendir(new(false, 0m, 0m, "kapida-nakit", false));
        var h = LegacyReturnMappings.Durum(0m, 0m, 0m, kural);
        Assert.AreEqual(ReturnConstants.StatusClosed, h.Status);
        Assert.AreEqual(ReturnConstants.RefundNotApplicable, h.RefundStatus);
        Assert.AreEqual(IadeOdemeKurali.NedenKapidaTahsilatYok, h.RefundNotApplicableReason);
        Assert.AreEqual(0m, h.RefundAmount);
    }

    [TestMethod]
    public void Pazaryeri_AlacakYok_Closed_Marketplace()   // R10 — dfplatforms.iadeOdemesiYap=0 ile aynı sonuç
    {
        var kural = IadeOdemeKurali.Degerlendir(new(true, 900m, 0m, "kart", true));
        var h = LegacyReturnMappings.Durum(0m, 0m, 900m, kural);
        Assert.AreEqual(ReturnConstants.StatusClosed, h.Status);
        Assert.AreEqual(IadeOdemeKurali.NedenPazaryeri, h.RefundNotApplicableReason);
    }

    [TestMethod]
    public void TahsilatVarAlacakHenuzYok_Received_Pending_UstSinirlaKirpilir()
    {
        var kural = IadeOdemeKurali.Degerlendir(new(false, 300m, 0m, "kart", true));
        var h = LegacyReturnMappings.Durum(0m, 0m, 450m, kural);
        Assert.AreEqual(ReturnConstants.StatusReceived, h.Status);
        Assert.AreEqual(300m, h.RefundAmount);
    }
}
