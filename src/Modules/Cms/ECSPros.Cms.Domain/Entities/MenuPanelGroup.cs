using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Cms.Domain.Entities;

public class MenuPanelGroup : BaseEntity
{
    public Guid MegaPanelId { get; set; }
    public Dictionary<string, string>? NameI18n { get; set; }
    public int ColumnIndex { get; set; }
    public int ColumnSpan { get; set; } = 1;
    public bool ShowTitle { get; set; } = true;
    public Dictionary<string, object>? TitleStyle { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public MenuMegaPanel MegaPanel { get; set; } = null!;
    public ICollection<MenuPanelItem> Items { get; set; } = new List<MenuPanelItem>();
}
