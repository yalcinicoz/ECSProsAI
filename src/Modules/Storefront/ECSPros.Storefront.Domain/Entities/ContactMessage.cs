using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// F3: iletişim formu mesajı (kullanıcı kararı: e-posta değil DB kaydı; ileride admin
/// panelden listelenir). Misafir de gönderebilir — MemberId yalnız girişli üyede dolar.
/// Status: new → read (admin akışı ileri iş).
/// </summary>
public class ContactMessage : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid? MemberId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Subject { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = "new";
}
