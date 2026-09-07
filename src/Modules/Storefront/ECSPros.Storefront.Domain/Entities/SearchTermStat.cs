using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Popüler aramalar (2026-09-01, kullanıcı kararı) — arama terimlerinin GÜN kovalı sayacı.
/// /urunler (SSR) ve /api/store/catalog/products aramaları normalize edilip
/// (trim + küçük harf + tek boşluk) sayılır; bot UA'ları ve sayfa>1 tekrarları sayılmaz.
/// Popüler liste = son 30 günün toplamı (eski moda takılı kalmaz); 90 günden eski kovalar
/// yazım sırasında fırsatçı temizlenir. Artırım çoklu-düğüm güvenli tek SQL upsert'tir
/// (AramaTerimIzleyici — EF değil, ON CONFLICT DO UPDATE).
/// </summary>
public class SearchTermStat : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    /// <summary>Normalize terim (≤60 karakter).</summary>
    public string Term { get; set; } = string.Empty;
    /// <summary>Gün kovası (UTC tarihi).</summary>
    public DateOnly Day { get; set; }
    public int Count { get; set; }
    /// <summary>2026-09-07: ziyaretçi anahtarı (üye kimliği ya da IP+UA özeti) — popüler liste "birden fazla kişi"
    /// ölçütü için; kova artık (platform, terim, gün, ziyaretçi). Eski satırlarda boş dize.</summary>
    public string VisitorHash { get; set; } = string.Empty;
    /// <summary>2026-09-07: aramanın getirdiği sonuç sayısı (kovadaki en yüksek değer) — popüler listeye yalnız
    /// sonuç getiren aramalar girer. Null = eski kayıt (bilinmiyor).</summary>
    public int? ResultCount { get; set; }
}
