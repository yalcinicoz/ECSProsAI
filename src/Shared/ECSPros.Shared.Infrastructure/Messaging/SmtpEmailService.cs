using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECSPros.Shared.Infrastructure.Messaging;

/// <summary>
/// H8: IEmailService'in gerçek implementasyonu — SMTP. Ayar çözümleme sırası:
/// 1) DB — ISmtpSettingsProvider (admin'in girdiği smtp servis tanımı; şifreli saklanır),
/// 2) config — Email:Smtp:{Host,Port=587,User,Password,From,FromName,UseSsl=true},
/// 3) ikisi de yoksa e-posta gönderilmez, LogEmailService biçiminde loglanır (site
/// e-postasız da çalışır — güvenlik ağı).
/// Gönderim hatası FIRLATILIR — çağıran (bildirim tüketicileri) yakalayıp kaydı
/// 'gönderilmedi' bırakır; iş akışları e-posta hatasıyla düşmemelidir (çağıran sözleşmesi).
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly ISmtpSettingsProvider? _settingsProvider;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger,
        ISmtpSettingsProvider? settingsProvider = null)
    {
        _configuration = configuration;
        _logger = logger;
        _settingsProvider = settingsProvider;
    }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        => SendAsync([to], subject, htmlBody, ct);

    public async Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken ct = default)
    {
        var alicilar = recipients.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
        if (alicilar.Count == 0) return;

        var settings = _settingsProvider is null ? null : await _settingsProvider.GetAsync(ct);
        settings ??= FromConfiguration();

        if (settings is null)
        {
            // SMTP hiç yapılandırılmamış — eski LogEmailService davranışı (log biçimi aynı).
            _logger.LogInformation("[EMAIL] To: {Recipients} | Subject: {Subject}",
                string.Join(", ", alicilar), subject);
            return;
        }

        var from = settings.From ?? settings.User ?? "noreply@localhost";

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        if (!string.IsNullOrWhiteSpace(settings.User))
            client.Credentials = new NetworkCredential(settings.User, settings.Password);

        using var mesaj = new MailMessage
        {
            From = new MailAddress(from, settings.FromName ?? "ECSPros"),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        foreach (var alici in alicilar)
            mesaj.To.Add(alici);

        try
        {
            await client.SendMailAsync(mesaj, ct);
            _logger.LogInformation("[EMAIL] To: {To} | Subject: {Subject} (SMTP gönderildi)",
                string.Join(", ", mesaj.To.Select(t => t.Address)), subject);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP gönderimi başarısız: {Subject} → {To}",
                subject, string.Join(", ", mesaj.To.Select(t => t.Address)));
            throw;
        }
    }

    private SmtpSettings? FromConfiguration()
    {
        var host = _configuration["Email:Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host)) return null;

        return new SmtpSettings(
            host,
            _configuration.GetValue("Email:Smtp:Port", 587),
            _configuration["Email:Smtp:User"],
            _configuration["Email:Smtp:Password"],
            _configuration["Email:Smtp:From"],
            _configuration["Email:Smtp:FromName"],
            _configuration.GetValue("Email:Smtp:UseSsl", true));
    }
}
