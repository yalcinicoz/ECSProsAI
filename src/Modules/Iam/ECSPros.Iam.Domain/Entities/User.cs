using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Iam.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public Guid? FirmId { get; set; }
    public string Department { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>SÜPER ADMİN (2026-09-09, karar K5): permission değil, kullanıcı üzerinde ayrı
    /// sistem özelliği. Tüm permission/kanal/alan kontrollerini bypass eder — ama işlemleri
    /// yine audit edilir. Kurallar (tasarım §K): yalnız süper admin atar/kaldırır, kullanıcı
    /// KENDİ bayrağını kaldıramaz, sistemde en az bir süper admin kalmalıdır.</summary>
    public bool IsSuperAdmin { get; set; } = false;
    public DateTime? LastLoginAt { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public bool MustChangePassword { get; set; } = false;
    public Dictionary<string, object>? Preferences { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
}
