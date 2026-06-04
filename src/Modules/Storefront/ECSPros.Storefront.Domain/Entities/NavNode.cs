using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Bir navigasyon menüsündeki tek düğüm.
/// nodeType=category → categoryId üzerinden ürün listesi
/// nodeType=link     → harici/dahili URL
/// nodeType=label    → kendi sayfası olmayan grup başlığı
/// </summary>
public class NavNode : BaseEntity
{
    public Guid NavigationMenuId { get; set; }
    public Guid? ParentNavNodeId { get; set; }
    public Guid? ChannelCategoryId { get; set; }
    public ChannelCategory? ChannelCategory { get; set; }

    // Kanal özelinde kimlik — null ise Category.NameI18n kullanılır
    public Dictionary<string, string>? NameOverrideI18n { get; set; }
    public string? Slug { get; set; }

    // Sunum
    public string? ImageUrl { get; set; }
    public string? BadgeLabel { get; set; }
    public string? Icon { get; set; }
    public bool OpenInNewTab { get; set; }

    // Düğüm tipi
    public string NodeType { get; set; } = "category"; // category | link | label
    public string? CustomUrl { get; set; }

    // SEO
    public Dictionary<string, string>? SeoTitleI18n { get; set; }
    public Dictionary<string, string>? SeoDescriptionI18n { get; set; }
    public string? CanonicalUrl { get; set; }
    public string? OgImageUrl { get; set; }
    public Dictionary<string, string>? OgTitleI18n { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public NavigationMenu NavigationMenu { get; set; } = null!;
    public NavNode? Parent { get; set; }
    public ICollection<NavNode> Children { get; set; } = new List<NavNode>();
}
