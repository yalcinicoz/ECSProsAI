using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Cms.Domain.Entities;

public class MenuMegaPanel : BaseEntity
{
    public Guid MenuItemId { get; set; }
    public string? Name { get; set; }
    public string LayoutType { get; set; } = string.Empty;
    public int ColumnCount { get; set; } = 4;
    public string? BackgroundColor { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string? CustomCss { get; set; }
    public bool IsActive { get; set; } = true;

    public SiteMenuItem MenuItem { get; set; } = null!;
    public ICollection<MenuPanelGroup> Groups { get; set; } = new List<MenuPanelGroup>();
}
