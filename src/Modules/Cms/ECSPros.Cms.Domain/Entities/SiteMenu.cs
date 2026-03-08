using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Cms.Domain.Entities;

public class SiteMenu : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string MenuType { get; set; } = string.Empty;
    public string DisplayStyle { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<SiteMenuItem> Items { get; set; } = new List<SiteMenuItem>();
}
