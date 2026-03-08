using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Cms.Domain.Entities;

public class SiteMenuItem : BaseEntity
{
    public Guid MenuId { get; set; }
    public Guid? ParentId { get; set; }
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string ItemType { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }
    public string? CustomUrl { get; set; }
    public string? Icon { get; set; }
    public string? ImageUrl { get; set; }
    public bool OpenInNewTab { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public SiteMenu Menu { get; set; } = null!;
    public SiteMenuItem? Parent { get; set; }
    public ICollection<SiteMenuItem> Children { get; set; } = new List<SiteMenuItem>();
    public MenuMegaPanel? MegaPanel { get; set; }
}
