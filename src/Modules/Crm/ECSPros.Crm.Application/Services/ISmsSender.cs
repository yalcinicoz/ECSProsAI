namespace ECSPros.Crm.Application.Services;

/// <summary>D4: SMS gönderim portu — Application katmanı sağlayıcıyı bilmez.
/// API tarafında Shared.Infrastructure'daki ISmsService'e köprülenir
/// (dev'de LogSmsService; gerçek sağlayıcı seçimi kullanıcı kararı).</summary>
public interface ISmsSender
{
    Task SendAsync(string phone, string message, CancellationToken ct = default);
}
