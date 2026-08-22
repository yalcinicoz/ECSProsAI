using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// Çerez/consent tercih günlüğü (İE-6 Faz F-5, 2026-08-22): banner/ayar ekranındaki her tercih
/// değişikliği bir satır — GDPR/KVKK ispatı (12 ay saklanır, worker temizler). ConsentId tarayıcıdaki
/// ms_consent çerezinin kimliğidir; üye girişliyse MemberId de yazılır ve sonraki girişlerde üyenin
/// SON tercihi cihazlar arası senkronlanır (latest by MemberId). IP yalnız SHA256 hash'li.
/// </summary>
public class TrackingConsentLog : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public string ConsentId { get; set; } = string.Empty;
    public Guid? MemberId { get; set; }
    public bool Analytics { get; set; }
    public bool Ads { get; set; }
    public bool Personalization { get; set; }
    /// <summary>banner | settings | member_sync | mobile</summary>
    public string Source { get; set; } = "banner";
    public string? IpHash { get; set; }
    public string? UserAgent { get; set; }
}
