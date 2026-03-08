using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Cms.Domain.Entities;

public class MenuPanelItem : BaseEntity
{
    public Guid PanelGroupId { get; set; }
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Dictionary<string, string>? DescriptionI18n { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }
    public string? CustomUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImagePosition { get; set; }
    public string? BadgeText { get; set; }
    public string? BadgeColor { get; set; }
    public string? CustomHtml { get; set; }
    public string? GenderFilter { get; set; }
    public bool OpenInNewTab { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public MenuPanelGroup PanelGroup { get; set; } = null!;
}
