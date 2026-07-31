using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Promotion.Domain.Entities;

/// <summary>
/// Kampanya = bir PLATFORMUN bir kampanya tipini uyguladığı örnek (F1). Tip şablonunu (Settings)
/// doldurur, geçerlilik/öncelik/ürün kapsamını platform belirler. Ürün ilişkilendirme kategori
/// mekanizmasıyla birebir: FillType (all/manual/filter/mixed) + FilterDef (CategoryFilterRules) +
/// materyalize CampaignProducts. (Bkz. docs/kampanya-uctan-uca-plani.md §2.5)
/// </summary>
public class Campaign : BaseEntity
{
    public Guid CampaignTypeId { get; set; }

    /// <summary>Kampanyayı uygulayan platform (tekil). Kopyalayarak başka platforma çoğaltılır.</summary>
    public Guid FirmPlatformId { get; set; }

    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Dictionary<string, string>? DescriptionI18n { get; set; }

    /// <summary>Kartta/listede gösterilecek kısa kampanya etiketi/rozeti (ör. "Süper Fırsat").</summary>
    public string? BadgeLabel { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }

    /// <summary>Tip SettingsSchema'sına göre doldurulan parametre değerleri (jsonb).</summary>
    public Dictionary<string, object> Settings { get; set; } = new();

    /// <summary>Ürün kapsamı doldurma tipi — kategoriyle aynı: all | manual | filter | mixed.</summary>
    public string FillType { get; set; } = "all";

    /// <summary>CategoryFilterRules ile uyumlu JSONB filtre tanımı (FillType=filter/mixed).</summary>
    public Dictionary<string, object>? FilterDef { get; set; }

    public CampaignType CampaignType { get; set; } = null!;
    public ICollection<CampaignProduct> Products { get; set; } = new List<CampaignProduct>();
    public ICollection<CampaignExclusion> Exclusions { get; set; } = new List<CampaignExclusion>();
    public ICollection<CampaignPlatform> Platforms { get; set; } = new List<CampaignPlatform>();
}
