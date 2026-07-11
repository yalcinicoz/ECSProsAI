using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// G1: vitrin/kişiselleştirme sisteminin TASLAK blok kaydı (spec: anasayfa-dizayn-yönetimi.txt).
/// Admin bu tabloda çalışır; canlı site bu tabloyu OKUMAZ — "Yayınla" ile üretilen
/// PublishedSnapshot.JsonData'yı okur (taslak/yayın ayrımı baştan).
/// Placement bloğun hangi yerleşimde durduğunu söyler (2026-07-07 kararı: anasayfa,
/// global üst duyuru alanı, ürün listesi üst/alt, ürün detay alt, sepet/teslimat/ödeme).
/// RuleJson blok seviyesi segment kuralıdır (G-M2 kural motoru; null = herkese görünür —
/// spec: default öğe kavramı YOK). ConfigJson tipe özgü yapılandırma taşır: carousel
/// teması/flash bitiş zamanı, ürün kaynağı+filtre+limit+sıralama (G3 motoru),
/// koleksiyon kaynağı, "Tümünü Gör" linki, banner mobil-carousel bayrağı vb.
/// </summary>
public class PageBlock : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public string Placement { get; set; } = "homepage"; // homepage | global-top | list-top | list-bottom | detail-bottom | cart | checkout-delivery | checkout-payment
    public string BlockType { get; set; } = string.Empty; // banner | slider | story | carousel | infinity | tabs | collection | categories | brands | instagram | announcement
    /// <summary>Tip içi şablon: banner tekli|ikili|uclu|dortlu|besli|reklam; carousel standart|ozel-fiyat|flash.</summary>
    public string? Template { get; set; }
    public Dictionary<string, string> TitleI18n { get; set; } = new();
    public Dictionary<string, string>? SubtitleI18n { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    /// <summary>Aynı anda birden çok içerik eşleşirse blok tipine göre seçim/ikincil sıralama anahtarı (spec).</summary>
    public int Priority { get; set; }
    public string? RuleJson { get; set; }
    public string? ConfigJson { get; set; }

    public ICollection<PageBlockItem> Items { get; set; } = new List<PageBlockItem>();
}
