using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// G1: taslak blok öğesi — slide, story öğesi, tab, banner öğesi veya duyuru satırı.
/// Kural seviyesi spec'e göre öğe bazlıdır (slider/story/duyuru: yalnız öğe;
/// tabs: blok + öğe; banner: yalnız blok — banner öğeleri layout bütünlüğü için
/// kuralsız tutulur, gizlenmeleri şablonu kırar). RuleJson null = herkese görünür.
/// ConfigJson öğeye özgü yapılandırma taşır: tab'ın ürün kaynağı/filtresi/limiti,
/// story video URL'i, banner hücre boyut ipucu vb.
/// </summary>
public class PageBlockItem : BaseEntity
{
    public Guid PageBlockId { get; set; }
    public PageBlock? PageBlock { get; set; }

    /// <summary>Öğe başlığı: slide başlığı / story adı / tab adı / duyuru metni / banner alt metni.</summary>
    public Dictionary<string, string> TitleI18n { get; set; } = new();
    public Dictionary<string, string>? SubtitleI18n { get; set; }
    public string? ImageUrl { get; set; }        // desktop görsel
    public string? MobileImageUrl { get; set; }  // mobil görsel (spec: slide mobil/desktop ayrı)
    public string? VideoUrl { get; set; }        // story video (spec: video/görsel)
    public string? LinkUrl { get; set; }
    public bool OpenInNewTab { get; set; }
    public Dictionary<string, string>? ButtonTextI18n { get; set; }
    public string? BadgeLabel { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public int Priority { get; set; }
    public string? RuleJson { get; set; }
    public string? ConfigJson { get; set; }
}
