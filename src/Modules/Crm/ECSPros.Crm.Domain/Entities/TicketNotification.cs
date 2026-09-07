using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

/// <summary>
/// Panel bildirimi (eski cm_bildirim; K2). Kind: created_by (kaydı açan) | tagged (etiketlendiniz) |
/// tagged_followup (etiketlendiğiniz kayda işlem) | participant (işlem yaptığınız kayda işlem).
/// SeenAt = bildirim listesinde gördü; OpenedAt = kayda girdi. İkisi de doluysa söner.
/// </summary>
public class TicketNotification : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid TicketId { get; set; }
    public long TrackingNo { get; set; }
    public Guid? ActivityId { get; set; }
    public string Kind { get; set; } = "participant";
    public string Message { get; set; } = string.Empty;
    public DateTime? SeenAt { get; set; }
    public DateTime? OpenedAt { get; set; }
    public int? LegacyId { get; set; }                   // BildirimID
}
