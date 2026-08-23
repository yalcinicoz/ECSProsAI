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

    // K2 (kanal seçimi — satış görünürlüğü M2): ürünün bu kanalda satılıp satılmayacağı.
    // Opt-out semantiği: satır yok VEYA IsActive=true → kanalda satılır; IsActive=false →
    // kanaldan çıkarılmış (listelerden ve satıştan düşer). Legacy plurunler.satisaAcik.
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // F1 kapsam katmanı (docs/satis-kanali-ortak-kurgu.md §3, K2/K3/K10). Kanalın ChannelScope'u
    // filter|mixed ise ürün yalnız InScope=true satırıyla kanaldadır; all (ya da scope yok) ise satır
    // yokluğu "kapsamda" demektir (opt-out korunur). ScopeSource: legacy (F1 öncesi satır) | filter
    // (sync yazdı) | manual (personel ekledi). IsExcluded: personelin KALICI kapsam-dışı kararı —
    // sync geri eklemez; IsActive=false'tan (kanal kararı) ayrı katmandır.
    public bool InScope { get; set; } = true;
    public string ScopeSource { get; set; } = "legacy";
    public bool IsExcluded { get; set; }

    // B11 (K8): tarih aralıklı "öne çıkar" — FeaturedFrom dolu ve aralık bugünü kapsıyorsa
    // ürün listelerde öne alınır (varsayılan sırada) ve kartta "Sponsorlu" rozeti görünür.
    // FeaturedUntil null = süresiz. Tam reklam modülü kapsam dışı (K8).
    public DateTime? FeaturedFrom { get; set; }
    public DateTime? FeaturedUntil { get; set; }

    public bool IsFeaturedAt(DateTime now) =>
        FeaturedFrom.HasValue && FeaturedFrom.Value <= now
        && (!FeaturedUntil.HasValue || FeaturedUntil.Value >= now);

    // K3 (kanalda durdurma — satış görünürlüğü M3): kanalda satılan ürünü anlık VEYA
    // [başlangıç, bitiş] penceresiyle durdurma. SaleStoppedFrom dolu ve an aralıktaysa ürün
    // o kanalda satıştan düşer; pencere bitince sorgu-zamanı otomatik geri açılır (job yok).
    // SaleStoppedUntil null = süresiz durdurma. Legacy plurunler.yayinda (=false → durdurulmuş).
    public DateTime? SaleStoppedFrom { get; set; }
    public DateTime? SaleStoppedUntil { get; set; }

    public bool IsSaleStoppedAt(DateTime now) =>
        SaleStoppedFrom.HasValue && SaleStoppedFrom.Value <= now
        && (!SaleStoppedUntil.HasValue || SaleStoppedUntil.Value >= now);
}
