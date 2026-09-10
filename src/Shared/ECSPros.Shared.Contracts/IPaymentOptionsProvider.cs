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
    decimal CodMaxOrderTotal,   // 0 = üst sınır yok
    // Taksit (2026-09-10, kullanıcı kararı): "installmentSource" = provider (ödeme aracısının tablosu,
    // varsayılan — bugünkü davranış) | own (kanalın kendi tablosu "installmentTable": [{count, rate}]).
    // Kendi tabloda müşteriye yansıyan vade farkı TaksitKurali ile hesaplanır; aracının komisyonu bize kalır.
    string InstallmentSource = TaksitKurali.KaynakOdemeAracisi,
    IReadOnlyList<TaksitTablosuSatiri>? InstallmentTable = null)
{
    public static readonly IReadOnlyList<string> TumYontemler = ["kart", "kapida-nakit", "kapida-kart"];

    public bool YontemAcik(string yontem) => EnabledMethods.Contains(yontem);

    /// <summary>Kanal kendi taksit tablosunu kullanıyor mu (aracının oranları yerine).</summary>
    public bool KendiTaksitTablosu => InstallmentSource == TaksitKurali.KaynakKendiTablomuz;
}

public interface IPaymentOptionsProvider
{
    /// <summary>Platformun ödeme seçeneklerini döner; platform bulunamazsa varsayılanlar.</summary>
    Task<PaymentOptions> GetAsync(Guid firmPlatformId, CancellationToken ct = default);
}
