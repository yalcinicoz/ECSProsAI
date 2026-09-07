using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

/// <summary>
/// Kayıt üzerindeki işlem (eski cm_crm_yapilan_islemler, Gizle=0 satırları). Tek formda: içerik + yeni durum +
/// (isteğe bağlı) tek personel etiketleme. Etiketleme sahiplik değiştirmez, bilgilendirir (eski davranış).
/// PreviousStatusId dolu ise durum bu işlemde değişmiştir.
/// </summary>
public class TicketActivity : BaseEntity
{
    public Guid TicketId { get; set; }
    public string BodyHtml { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public List<string> Attachments { get; set; } = new();
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid StatusId { get; set; }                   // işlem sonrası durum
    public Guid? PreviousStatusId { get; set; }          // değiştiyse önceki durum
    public Guid? TaggedUserId { get; set; }
    public string? TaggedUserName { get; set; }
    public int? LegacyId { get; set; }                   // YapilanIslemID

    public Ticket Ticket { get; set; } = null!;
}
