using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Push gönderim kuyruğu + logu (§7) ve uygulama içi "Bildirimlerim" satırı (docs/BILDIRIMLERIM_BACKEND_ISTEGI.md, 2026-09-08).
/// Cihaz başına bir satır; (DedupId, DeviceId) benzersiz → aynı olay aynı cihaza iki kez gitmez. Üyeye hedeflenen bildirim
/// FCM'e gidemese bile (cihaz/izin yok) DeviceId=null + Status=skipped satırla listede görünür. Token ham hâliyle tutulmaz
/// (TokenHash). Status queued | sent | failed | skipped. Kullanıcı silmesi DismissedAt'tir (BaseEntity soft delete admin logunu
/// gizlerdi; bu yüzden ayrı kolon).
/// </summary>
public class PushNotification : BaseEntity
{
    public Guid? MemberId { get; set; }
    public Guid? DeviceId { get; set; }                     // push_devices.Id — yalnız listeye giren (cihazsız) satırda null
    public Guid FirmPlatformId { get; set; }
    public string Platform { get; set; } = "android";
    public string TokenHash { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Class { get; set; } = "transactional";
    public string DedupId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Link { get; set; } = "/";
    public string? ImageUrl { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();   // gönderilen data (jsonb)
    public string Status { get; set; } = "queued";
    public string? FcmMessageId { get; set; }
    public string? ErrorCode { get; set; }
    public int Attempts { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? OpenedAt { get; set; }

    // Uygulama içi liste
    public bool Inbox { get; set; } = true;                 // listede görünsün mü (şablondan)
    public string Icon { get; set; } = "info";              // §5 ikon anahtarı
    public bool DismissOnOpen { get; set; }                 // okununca listeden düşsün (şablondan)
    public DateTime ExpiresAt { get; set; }                 // listede görünme süresi sonu
    public DateTime? ReadAt { get; set; }                   // kullanıcı okudu (push açılması da set eder)
    public DateTime? DismissedAt { get; set; }              // kullanıcı listeden sildi
}
