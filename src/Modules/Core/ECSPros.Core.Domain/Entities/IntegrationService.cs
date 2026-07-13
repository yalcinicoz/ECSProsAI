using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

public class IntegrationService : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string ServiceType { get; set; } = string.Empty; // marketplace, cargo, invoice_integrator, payment, sms, email, visual_search, erp, other
    public bool IsAvailable { get; set; } = false;
    /// <summary>Admin form alan şeması — camelCase JSON, List&lt;PlatformSchemaField&gt;
    /// (PlatformType.SettingsSchemaJson kalıbı): section=credentials → şifreli
    /// Credentials'a, settings → Settings jsonb'sine yazılır.</summary>
    public string? SettingsSchemaJson { get; set; }

    // H2: kargo servisleri için görsel kimlik + takip linki — firma sözleşmesine değil
    // kargo firmasının kendisine ait (tüm firmalar için aynı); diğer tiplerde null.
    public string? LogoUrl { get; set; }
    public string? TrackingUrlTemplate { get; set; } // {trackingNumber} yer tutucusu

    public ICollection<FirmPlatformIntegration> PlatformIntegrations { get; set; } = new List<FirmPlatformIntegration>();
}
