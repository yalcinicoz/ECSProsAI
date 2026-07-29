using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Requests.Domain.Entities;

/// <summary>
/// Personelin proje ile ilgili talepleri (2026-07-23). Durum akışı:
/// new → evaluation → planned → in_progress → testing → done;
/// new/evaluation'dan rejected'a, terminal olmayan her durumdan cancelled'a çıkılabilir
/// (geçiş haritası ChangeRequestStatusCommand'dadır). Talep eden/atanan ad alanları
/// bilinçli denormalizedir — IAM modülüne çapraz join yapılmaz (modüler monolith sınırı).
/// </summary>
public class ProjectRequest : BaseEntity
{
    public string Code { get; set; } = string.Empty;          // TLP-2026-0001
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;      // core lookup: request_category
    public string Priority { get; set; } = "normal";          // low | normal | high | critical
    public string Status { get; set; } = "new";
    public Guid RequestedBy { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public Guid? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public DateOnly? DueDate { get; set; }                    // termin
    public DateTime? CompletedAt { get; set; }                // done/rejected/cancelled anı

    public ICollection<RequestActivity> Activities { get; set; } = new List<RequestActivity>();
}
