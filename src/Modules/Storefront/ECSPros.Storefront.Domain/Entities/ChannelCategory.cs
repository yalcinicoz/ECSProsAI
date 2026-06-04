using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Kanala özgü kategori. Ürün listeleme sayfalarını, filtrelerini ve menü/banner
/// bağlantılarını tek bir yerde tanımlar.
/// </summary>
public class ChannelCategory : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid? ParentId { get; set; }

    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string Slug { get; set; } = string.Empty;

    /// <summary>draft | published | archived</summary>
    public string Status { get; set; } = "draft";

    /// <summary>manual | filter | mixed</summary>
    public string FillType { get; set; } = "manual";

    /// <summary>CategoryFilterRules ile uyumlu JSONB filtre tanımı. FillType=filter/mixed için kullanılır.</summary>
    public Dictionary<string, object>? FilterDef { get; set; }

    public int SortOrder { get; set; }
    public string? DisplayImageUrl { get; set; }
    public string? BadgeLabel { get; set; }

    // SEO
    public Dictionary<string, string>? MetaTitleI18n { get; set; }
    public Dictionary<string, string>? MetaDescriptionI18n { get; set; }
    public string? OgImageUrl { get; set; }
    public Dictionary<string, string>? OgTitleI18n { get; set; }

    public ChannelCategory? Parent { get; set; }
    public ICollection<ChannelCategory> Children { get; set; } = new List<ChannelCategory>();
    public ICollection<ChannelCategoryGroup> CategoryGroups { get; set; } = new List<ChannelCategoryGroup>();
    public ICollection<ChannelCategoryProduct> CategoryProducts { get; set; } = new List<ChannelCategoryProduct>();
}
