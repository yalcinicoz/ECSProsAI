using Microsoft.Extensions.Logging;

namespace ECSPros.Shared.Infrastructure.Messaging;

/// <summary>Development/stub SMS service — logs messages instead of sending them.</summary>
public class LogSmsService : ISmsService
{
    private readonly ILogger<LogSmsService> _logger;

    public LogSmsService(ILogger<LogSmsService> logger) => _logger = logger;

    public Task SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("[SMS] To: {Phone} | Message: {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }
}
