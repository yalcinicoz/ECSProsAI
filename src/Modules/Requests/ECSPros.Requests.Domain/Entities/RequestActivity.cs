using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Requests.Domain.Entities;

/// <summary>
/// Talep zaman akışı: yorumlar ve sistemin ürettiği süreç kayıtları (durum değişimi,
/// atama, güncelleme) tek tabloda — detay sayfası akışı buradan kronolojik okur.
/// </summary>
public class RequestActivity : BaseEntity
{
    public Guid RequestId { get; set; }
    public string ActivityType { get; set; } = "comment";     // comment | created | status_change | assignment | updated
    public string? Comment { get; set; }
    public string? OldValue { get; set; }                     // status_change/assignment: önceki değer
    public string? NewValue { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public List<string> Attachments { get; set; } = new();    // /media/talepler/... yolları (jsonb)

    public ProjectRequest Request { get; set; } = null!;
}
