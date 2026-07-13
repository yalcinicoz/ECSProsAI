namespace ECSPros.Shared.Infrastructure.Messaging;

/// <summary>
/// SMTP ayarlarının kaynağı — DB'deki platform servis tanımından (FirmPlatformIntegration,
/// ServiceType=email) çözülür; Api katmanı implemente eder. Kayıt yoksa null döner ve
/// SmtpEmailService config (Email:Smtp:*) yedeğine düşer.
/// </summary>
public interface ISmtpSettingsProvider
{
    Task<SmtpSettings?> GetAsync(CancellationToken ct = default);
}

public record SmtpSettings(
    string Host,
    int Port,
    string? User,
    string? Password,
    string? From,
    string? FromName,
    bool UseSsl);
