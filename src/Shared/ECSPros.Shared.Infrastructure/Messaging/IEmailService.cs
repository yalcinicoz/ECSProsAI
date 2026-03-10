namespace ECSPros.Shared.Infrastructure.Messaging;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken ct = default);
}
