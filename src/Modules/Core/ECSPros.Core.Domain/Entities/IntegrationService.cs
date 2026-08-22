using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

public class IntegrationService : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    /// <summary>marketplace, cargo, invoice_integrator, payment, sms, email, visual_search, social_login,
    /// erp, other; takip (İE-1, 2026-08-22): analytics, tag_manager, ads, merchant, search_console,
    /// meta, tiktok, pinterest, microsoft_ads, clarity</summary>
    public string ServiceType { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = false;
    /// <summary>Admin form alan şeması — camelCase JSON, List&lt;PlatformSchemaField&gt;
    /// (PlatformType.SettingsSchemaJson kalıbı): section=credentials → şifreli
    /// Credentials'a, settings → Settings jsonb'sine yazılır.</summary>
    public string? SettingsSchemaJson { get; set; }

    // H2: kargo servisleri için görsel kimlik + takip linki — firma sözleşmesine değil
    // kargo firmasının kendisine ait (tüm firmalar için aynı); diğer tiplerde null.
    public string? LogoUrl { get; set; }
    public string? TrackingUrlTemplate { get; set; } // {trackingNumber} yer tutucusu

    // F3 (2026-07-19): kargo entegrasyon kodu üretim stratejisi — taşıyıcıya özel kural.
    // free: serbest (paket no + opsiyonel önek) · pattern: free + uzunluk/karakter kuralı
    // range: firmaya tahsisli barkod aralığından (PTT) · external: kod dışarıdan gelir
    public string? CargoCodeStrategy { get; set; }
    public int? CargoCodeMinLength { get; set; }
    public int? CargoCodeMaxLength { get; set; }
    public string? CargoCodeCharset { get; set; } // numeric | alnum

    public ICollection<FirmPlatformIntegration> PlatformIntegrations { get; set; } = new List<FirmPlatformIntegration>();
}
