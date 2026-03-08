using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Iam.Domain.Entities;

public class UserPermission : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid PermissionId { get; set; }
    public string GrantType { get; set; } = "grant"; // grant, revoke
    public Guid? FirmId { get; set; }

    public User User { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
