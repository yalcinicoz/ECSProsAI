using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Iam.Domain.Entities;

public class AdminMenu : BaseEntity
{
    public Guid? ParentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string? Icon { get; set; }
    public string? Route { get; set; }
    public string? PermissionCode { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public AdminMenu? Parent { get; set; }
    public ICollection<AdminMenu> Children { get; set; } = new List<AdminMenu>();
}
