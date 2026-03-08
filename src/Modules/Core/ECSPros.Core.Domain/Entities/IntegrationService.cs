using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

public class IntegrationService : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string ServiceType { get; set; } = string.Empty; // marketplace, cargo, invoice_integrator, payment, sms, erp, other
    public bool IsAvailable { get; set; } = false;
    public Dictionary<string, object>? SettingsSchema { get; set; }

    public ICollection<FirmIntegration> FirmIntegrations { get; set; } = new List<FirmIntegration>();
}
