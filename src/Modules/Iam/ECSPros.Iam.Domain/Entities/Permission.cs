using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Iam.Domain.Entities;

public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Dictionary<string, string>? DescriptionI18n { get; set; }
    public string Module { get; set; } = string.Empty;
    public string PermissionType { get; set; } = string.Empty; // read, create, update, delete, manage
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
}
