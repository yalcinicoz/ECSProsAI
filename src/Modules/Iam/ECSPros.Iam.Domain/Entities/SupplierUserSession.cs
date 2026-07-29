using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Iam.Domain.Entities;

/// <summary>
/// iam.supplier_user_sessions — SupplierUser refresh oturumu (MemberSession kalıbı).
/// Refresh token opaque; SHA256 hash saklanır, rotasyonda eski oturum IsActive=false.
/// </summary>
public class SupplierUserSession : BaseEntity
{
    public Guid SupplierUserId { get; set; }
    public string RefreshTokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public SupplierUser SupplierUser { get; set; } = null!;
}
