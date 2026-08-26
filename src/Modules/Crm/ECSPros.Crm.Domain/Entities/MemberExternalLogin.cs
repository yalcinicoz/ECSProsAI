using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

/// <summary>
/// Üyenin dış OAuth kimlikleri (Google/Facebook vb.). Aynı üye birden çok
/// sağlayıcıyla giriş yapabilir; aynı sağlayıcı+kimlik çifti tekil tutulur
/// (unique index). E-posta, bağlama anındaki değeri sabitler (kaynak bilgisi).
/// </summary>
public class MemberExternalLogin : BaseEntity
{
    public Guid MemberId { get; set; }
    public string Provider { get; set; } = string.Empty;   // "google" | "facebook"
    public string ProviderUserId { get; set; } = string.Empty;
    public string? Email { get; set; }

    public Member Member { get; set; } = null!;
}
