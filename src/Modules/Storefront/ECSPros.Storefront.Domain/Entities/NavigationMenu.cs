using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Bir kanalın (FirmPlatform) adlandırılmış navigasyon menüsü.
/// Örnek: header, footer, sidebar. NavNode ağacının giriş noktası.
/// </summary>
public class NavigationMenu : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string MenuType { get; set; } = "header"; // header | footer | sidebar | custom
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<NavNode> Nodes { get; set; } = new List<NavNode>();
}
