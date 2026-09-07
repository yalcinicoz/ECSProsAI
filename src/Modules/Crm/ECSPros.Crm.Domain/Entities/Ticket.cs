using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

/// <summary>
/// Müşteri İlişkileri kaydı — talep/şikayet (eski cm_crm; plan docs/crm-musteri-iliskileri-plani.md v2).
/// Müşteri/sipariş bilgileri ANLIK GÖRÜNTÜ olarak tutulur (eski kayıtların çoğu bizde üye/sipariş olarak yok);
/// bulunduysa MemberId/OrderId bağı kurulur. TrackingNo eski biçim: unix saniye + sıra (K4). Personel adları
/// denormalize (IAM'a çapraz join yok — Requests modülü deseni).
/// </summary>
public class Ticket : BaseEntity
{
    public long TrackingNo { get; set; }
    public string Type { get; set; } = "request";              // complaint | request (konudan türer)
    public Guid SubjectId { get; set; }
    public Guid StatusId { get; set; }

    public Guid? MemberId { get; set; }
    public int? LegacyMemberId { get; set; }
    public string CustomerName { get; set; } = string.Empty;   // siparişten kopyalanır
    public string CustomerPhone { get; set; } = string.Empty;
    public string CallerName { get; set; } = string.Empty;     // arayan (müşteri olmayabilir)
    public string CallerPhone { get; set; } = string.Empty;

    public Guid? OrderId { get; set; }
    public string? OrderNumber { get; set; }                   // metin: eski numara ya da ORD-…
    public Guid? FirmPlatformId { get; set; }                  // siparişten; aynı numara birden çok kanaldaysa seçilir (K9)

    public string BodyHtml { get; set; } = string.Empty;       // temizlenmiş zengin metin
    public string BodyText { get; set; } = string.Empty;       // arama için düz metin
    public List<string> Attachments { get; set; } = new();     // /media/crm/... (jsonb)

    public Guid? CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
    public DateTime LastActivityAt { get; set; }
    public Guid? LastActivityId { get; set; }
    public int ActivityCount { get; set; }

    public bool IsHidden { get; set; }                         // eski Gizle
    public int? LegacyId { get; set; }                         // cm_crm.CRMID

    public TicketSubject Subject { get; set; } = null!;
    public TicketStatus Status { get; set; } = null!;
    public ICollection<TicketActivity> Activities { get; set; } = new List<TicketActivity>();
}
