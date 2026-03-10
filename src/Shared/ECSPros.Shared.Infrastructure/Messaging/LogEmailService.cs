using Microsoft.Extensions.Logging;

namespace ECSPros.Shared.Infrastructure.Messaging;

/// <summary>Development/stub email service — logs emails instead of sending them.</summary>
public class LogEmailService : IEmailService
{
    private readonly ILogger<LogEmailService> _logger;

    public LogEmailService(ILogger<LogEmailService> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogInformation("[EMAIL] To: {To} | Subject: {Subject}", to, subject);
        return Task.CompletedTask;
    }

    public Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogInformation("[EMAIL] To: {Recipients} | Subject: {Subject}", string.Join(", ", recipients), subject);
        return Task.CompletedTask;
    }
}
