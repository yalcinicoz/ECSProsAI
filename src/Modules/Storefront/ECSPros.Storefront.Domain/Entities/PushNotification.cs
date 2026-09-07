using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Push gönderim kuyruğu + logu (§7). Cihaz başına bir satır; (DedupId, DeviceId) benzersiz → aynı olay aynı cihaza
/// iki kez gitmez. Token ham hâliyle tutulmaz (TokenHash). Status queued | sent | failed | skipped.
/// </summary>
public class PushNotification : BaseEntity
{
    public Guid? MemberId { get; set; }
    public Guid DeviceId { get; set; }                      // push_devices.Id
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
}
