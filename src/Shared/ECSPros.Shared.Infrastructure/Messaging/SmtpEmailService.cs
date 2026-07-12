using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECSPros.Shared.Infrastructure.Messaging;

/// <summary>
/// H8: IEmailService'in ilk gerçek implementasyonu — SMTP. Yalnız `Email:Smtp:Host`
/// yapılandırıldığında kaydedilir (DependencyInjection), yoksa LogEmailService kalır.
/// Gönderim hatası FIRLATILIR — çağıran (bildirim tüketicileri) yakalayıp kaydı
/// 'gönderilmedi' bırakır; iş akışları e-posta hatasıyla düşmemelidir (çağıran sözleşmesi).
/// Config: Email:Smtp:{Host,Port=587,User,Password,From,FromName,UseSsl=true}
/// — kimlik bilgileri yalnız appsettings.Production.json'da (gitignore'lu).
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        => SendAsync([to], subject, htmlBody, ct);

    public async Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host = _configuration["Email:Smtp:Host"]!;
        var from = _configuration["Email:Smtp:From"] ?? _configuration["Email:Smtp:User"] ?? "noreply@localhost";
        var fromName = _configuration["Email:Smtp:FromName"] ?? "ECSPros";
        var user = _configuration["Email:Smtp:User"];

        using var client = new SmtpClient(host, _configuration.GetValue("Email:Smtp:Port", 587))
        {
            EnableSsl = _configuration.GetValue("Email:Smtp:UseSsl", true),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        if (!string.IsNullOrWhiteSpace(user))
            client.Credentials = new NetworkCredential(user, _configuration["Email:Smtp:Password"]);

        using var mesaj = new MailMessage
        {
            From = new MailAddress(from, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        foreach (var alici in recipients.Where(a => !string.IsNullOrWhiteSpace(a)))
            mesaj.To.Add(alici);

        if (mesaj.To.Count == 0) return;

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
}
