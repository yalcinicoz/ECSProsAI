namespace ECSPros.Shared.Infrastructure.Messaging;

/// <summary>
/// SMS ayarlarının kaynağı — DB'deki platform servis tanımından (FirmPlatformIntegration,
/// ServiceType=sms) çözülür; Api katmanı implemente eder (DbSmsSettingsProvider).
/// Kayıt yoksa null döner ve GesTelekomSmsService log yedeğine düşer
/// (site SMS'siz de çalışır — SMTP ile aynı güvenlik ağı).
/// </summary>
public interface ISmsSettingsProvider
{
    Task<SmsSettings?> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// <paramref name="ProviderCode"/>: IntegrationService.Code — kaydın hangi sağlayıcıya ait
/// olduğu (gestelekom, ...). Farklı SMS firmasına geçildiğinde yeni katalog kaydı + yeni
/// ISmsService implementasyonu açılır; ProviderCode uyuşmazlığı gönderimi log yedeğine düşürür.
/// </summary>
public record SmsSettings(
    string ProviderCode,
    string? ApiUrl,
    string Username,
    string Password,
    string Origin,
    bool IsNotification);
