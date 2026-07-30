namespace ECSPros.Api.Services.Store;

/// <summary>PayTR Direct API çözülmüş ayarları (kimlikler DB'de şifreli tutulur, burada
/// düz metin — bellekte, asla loglanmaz/serialize edilmez). testMode ŞU AN ZORUNLU AÇIK.</summary>
public record PaymentSettings(
    string MerchantId,
    string MerchantKey,
    string MerchantSalt,
    bool TestMode);

public interface IPaymentSettingsProvider
{
    /// <summary>Aktif "payment" tipli firma entegrasyonundan PayTR ayarlarını çözer;
    /// yoksa null (ödeme yapılandırılmamış).</summary>
    Task<PaymentSettings?> GetAsync(CancellationToken ct = default);
}
