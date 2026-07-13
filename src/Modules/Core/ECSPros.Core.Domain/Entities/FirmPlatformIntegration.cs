using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

/// <summary>
/// Firmanın bir dış servisle (kargo, SMTP, görsel arama, pazaryeri...) yapılandırılmış
/// anlaşması/hesabı. FirmPlatformId null ise firma geneli (tüm platformları kapsar);
/// dolu ise yalnız o platforma özeldir — çözümlemede platforma özel kayıt firma-geneline
/// tercih edilir. İletişim/sözleşme-no gibi serbest bilgiler kolon değil, servis
/// kataloğunun SettingsSchema'sında tanımlanıp Settings jsonb'sinde tutulur.
/// Credentials at-rest şifrelidir (Data Protection — Infrastructure value converter).
/// </summary>
public class FirmPlatformIntegration : BaseEntity
{
    public Guid FirmId { get; set; }
    public Guid IntegrationServiceId { get; set; }
    public Guid? FirmPlatformId { get; set; }
    public string? Name { get; set; }
    public Dictionary<string, object> Credentials { get; set; } = new();
    public Dictionary<string, object> Settings { get; set; } = new();
    public bool IsActive { get; set; } = true;

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "draft"; // draft, active, expired, cancelled
    public Dictionary<string, object>? Terms { get; set; }

    public Firm Firm { get; set; } = null!;
    public FirmPlatform? FirmPlatform { get; set; }
    public IntegrationService IntegrationService { get; set; } = null!;
}
