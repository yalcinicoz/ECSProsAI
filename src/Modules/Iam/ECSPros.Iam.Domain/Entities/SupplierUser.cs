using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Iam.Domain.Entities;

/// <summary>
/// iam.supplier_users — pazaryeri satıcısının (marketplace tedarikçi) İNSAN panel kullanıcısı.
/// Personel (User), site üyesi (Member) ve makine (ApiClient) kimliklerinden BAĞIMSIZ 4. kimlik türü.
/// Bir cari karta (accounts.current_accounts, AccountType=supplier + SupplierKind=marketplace) bağlanır;
/// parola akışıyla giriş yapar (type=supplier_user, owner_id=CurrentAccountId). Aynı cariye birden çok
/// kullanıcı açılabilir (S1: rolsüz — hepsi tam yetkili). ApiClient gibi cross-schema FK YOK; salt Guid.
/// </summary>
public class SupplierUser : BaseEntity
{
    public Guid CurrentAccountId { get; set; }               // accounts.current_accounts.Id (owner)
    public string Email { get; set; } = string.Empty;        // unique — giriş kimliği
    public string PasswordHash { get; set; } = string.Empty; // BCrypt
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public bool MustChangePassword { get; set; }
}
