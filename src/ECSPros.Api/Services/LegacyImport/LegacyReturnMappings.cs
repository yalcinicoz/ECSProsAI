using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Contracts;

namespace ECSPros.Api.Services.LegacyImport;

/// <summary>
/// Eski sistem iade verisinin yeni sözlüğe eşlenmesi — TEK yer (İade akışı planı, legacy düzeltmesi 2026-09-10).
///
/// Eski kaynak (ECSGYE OrderOperations.cs, 2026-09-10 kod+veri incelemesi):
///  • <c>opiadesiparisler.iadeTipi</c>: 1 = <b>Teslimatsız İade</b> (TeslimatsizIade: sipariş bütünü, kalem nedeni 9,
///    sipariş durumu "Teslim Edilemeden İade Geldi", iadeTutari = ödenmiş tahsilat toplamı);
///    2 = <b>Müşteri İadesi</b> (SiparisUrunIade: kalem bazlı, sipariş durumu değişmez).
///  • <c>durumu</c> hep 0, <c>uyeyeOdenenTutar/uyeyeOdemeTarihi/uyeyeOdemeTipi</c> hiç yazılmıyor (canlıda 173K
///    kayıtta tümü 0/NULL/1) → "üyeye ödendi" bilgisi <c>webuyeparalari</c>'ndan gelir
///    (iadeSiparislerId + musteriIstegi=2 satırları; odemeTarihi dolu = ödendi).
///  • <c>opiadeurunler.musteriIstegi</c>: 2 = İade (para iadesi), 1 = Değişim (üye bakiyesine alacak → cüzdan).
///  • <c>dfiadenedenleri</c>: 1 Belirsiz, 2 Beğenmedim, 3 Beden, 4 Defo, 5 Kalitesiz, 9 Teslim Edilmedi.
///  • Pazaryeri platformlarında (dfplatforms.iadeOdemesiYap=0) üyeye ödeme satırı hiç açılmaz (R10 ile aynı).
/// </summary>
public static class LegacyReturnMappings
{
    public const int RawTypeUndelivered = 1;
    public const int RawTypeCustomer = 2;
    public const int CustomerRequestExchange = 1;   // Değişim → bakiye/cüzdan
    public const int CustomerRequestRefund = 2;     // İade → para iadesi

    public static string ReasonCode(int sourceReasonId) => sourceReasonId switch
    {
        1 => "legacy_unspecified",
        2 => "legacy_disliked",
        3 => "legacy_size",
        4 => "legacy_defective",
        5 => "legacy_low_quality",
        9 => "legacy_not_delivered",
        _ => "legacy_unknown"
    };

    /// <summary>İade tipi sözlüğü (R1). Bilinmeyen ham tip tahmin edilmeden korunur.</summary>
    public static string ReturnType(int rawType) => rawType switch
    {
        RawTypeUndelivered => ReturnConstants.TypeUndelivered,
        RawTypeCustomer => ReturnConstants.TypeCustomer,
        _ => $"legacy_type_{rawType}"
    };

    /// <summary>Geri ödeme yöntemi: kalemlerin tamamı Değişim ise cüzdan; değilse siparişin ödeme yöntemine göre
    /// (kart → karta iade, kapıda → havale); eski sistemde IBAN'a havale yapılır.</summary>
    public static string RefundMethod(string? orderPaymentMethod, bool exchangeOnly)
        => exchangeOnly ? ReturnConstants.RefundMethodWallet : ReturnConstants.RefundMethodFor(orderPaymentMethod);

    /// <summary>Eski dfpaymenttypes → Order.PaymentMethod sözlüğü (LegacyOrderImportSlice.PaymentMethodValue ile aynı);
    /// hedef siparişte PaymentMethod boşsa (eski aktarım) kural için buradan türetilir.</summary>
    public static string? OrderPaymentMethod(int legacyPaymentTypeId) => legacyPaymentTypeId switch
    {
        1 => "kart",
        2 => "kapida-nakit",
        3 => "kapida-kart",
        _ => null
    };

    /// <summary>Türetilmiş hedef durumu.</summary>
    public sealed record HedefDurum(string Status, string RefundStatus, string? RefundNotApplicableReason, decimal RefundAmount, string ItemStatus);

    /// <summary>
    /// Eski iade = ürün depoya GELMİŞ kayıttır (IadeKabul ürünü okutunca açılır) → hedefte "teslim alındı" sonrası bir
    /// durumdur. Sıra: eski sistemde üyeye ödendi → <c>refunded</c>; eskide alacak açılmış ama ödenmemiş →
    /// <c>received</c>/pending; hiç alacak yoksa yeni kural (<see cref="IadeOdemeKurali"/>) karar verir:
    /// uygun → <c>received</c>/pending, değil → <c>closed</c>/not_applicable (kapıda ödeme teslimsiz, pazaryeri, tahsilat yok).
    /// </summary>
    public static HedefDurum Durum(
        decimal legacyPaidToMember, decimal legacyCreditToMember, decimal legacyAmount, IadeOdemeKurali.Sonuc kural)
    {
        if (legacyPaidToMember > 0m)
            return new(ReturnConstants.StatusRefunded, ReturnConstants.RefundCompleted, null, Math.Round(legacyPaidToMember, 2), "received");
        if (legacyCreditToMember > 0m)
            return new(ReturnConstants.StatusReceived, ReturnConstants.RefundPending, null, Math.Round(legacyCreditToMember, 2), "received");
        if (kural.Uygun)
            return new(ReturnConstants.StatusReceived, ReturnConstants.RefundPending, null, IadeOdemeKurali.TutarKirp(legacyAmount, kural.UstSinir), "received");
        return new(ReturnConstants.StatusClosed, ReturnConstants.RefundNotApplicable, kural.Neden, 0m, "received");
    }
}
