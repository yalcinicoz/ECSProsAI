using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Belirli bir kanalda (marketplace, bayi vb.) açıkça aktif edilen ürünler.
/// Navigasyon ağacına gerek olmayan kanallar (Trendyol, bayiler) için kullanılır.
/// </summary>
public class ChannelProduct : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid ProductId { get; set; }
    public Dictionary<string, string>? NameI18n { get; set; }
    public Dictionary<string, string>? ShortDescriptionI18n { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // B11 (K8): tarih aralıklı "öne çıkar" — FeaturedFrom dolu ve aralık bugünü kapsıyorsa
    // ürün listelerde öne alınır (varsayılan sırada) ve kartta "Sponsorlu" rozeti görünür.
    // FeaturedUntil null = süresiz. Tam reklam modülü kapsam dışı (K8).
    public DateTime? FeaturedFrom { get; set; }
    public DateTime? FeaturedUntil { get; set; }

    public bool IsFeaturedAt(DateTime now) =>
        FeaturedFrom.HasValue && FeaturedFrom.Value <= now
        && (!FeaturedUntil.HasValue || FeaturedUntil.Value >= now);
}
