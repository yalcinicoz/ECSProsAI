using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Iam.Domain.Entities;

public class UserRole : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid? FirmId { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
