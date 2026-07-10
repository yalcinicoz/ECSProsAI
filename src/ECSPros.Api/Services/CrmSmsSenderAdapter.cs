using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Infrastructure.Messaging;

namespace ECSPros.Api.Services;

/// <summary>D4: CRM'in ISmsSender portunu Shared.Infrastructure'daki ISmsService'e
/// köprüler (dev'de LogSmsService loglar; gerçek sağlayıcı seçilince yalnız
/// ISmsService implementasyonu değişir).</summary>
public class CrmSmsSenderAdapter(ISmsService smsService) : ISmsSender
{
    public Task SendAsync(string phone, string message, CancellationToken ct = default) =>
        smsService.SendAsync(phone, message, ct);
}
