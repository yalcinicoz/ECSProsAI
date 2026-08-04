namespace ECSPros.Shared.Contracts;

/// <summary>
/// Platformun sitede sunduğu ödeme seçenekleri (2026-08-04) — FirmPlatform.Settings
/// jsonb'sinden panelce yönetilir: "paymentMethods" (dizi), "codServiceFee",
/// "codMaxOrderTotal". Ayar yoksa güvenli varsayılanlar (üç yöntem açık, 50 TL bedel,
/// 3000 TL üst sınır) — mevcut davranış korunur.
/// </summary>
public record PaymentOptions(
    IReadOnlyList<string> EnabledMethods,
    decimal CodServiceFee,
    decimal CodMaxOrderTotal)   // 0 = üst sınır yok
{
    public static readonly IReadOnlyList<string> TumYontemler = ["kart", "kapida-nakit", "kapida-kart"];

    public bool YontemAcik(string yontem) => EnabledMethods.Contains(yontem);
}

public interface IPaymentOptionsProvider
{
    /// <summary>Platformun ödeme seçeneklerini döner; platform bulunamazsa varsayılanlar.</summary>
    Task<PaymentOptions> GetAsync(Guid firmPlatformId, CancellationToken ct = default);
}
