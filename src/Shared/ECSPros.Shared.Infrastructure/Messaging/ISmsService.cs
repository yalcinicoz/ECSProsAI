namespace ECSPros.Shared.Infrastructure.Messaging;

public interface ISmsService
{
    Task SendAsync(string phoneNumber, string message, CancellationToken ct = default);
}
