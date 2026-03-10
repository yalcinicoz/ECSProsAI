using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class MemberSession : BaseEntity
{
    public Guid MemberId { get; set; }
    public string RefreshTokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public Member Member { get; set; } = null!;
}
