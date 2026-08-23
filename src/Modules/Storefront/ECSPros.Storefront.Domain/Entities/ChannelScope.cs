using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// storefront.channel_scopes — bir satış kanalının ÜRÜN KAPSAMI tanımı (F1, docs/satis-kanali-ortak-kurgu.md §3.1, K2).
/// Kanal başına en çok bir satır. Satır yok ya da FillType=all → kapsam = tüm (görselli) katalog ürünleri
/// (bugünkü opt-out davranışı; channel_products satırı yoksa ürün kanaldadır).
/// FillType=filter|mixed → FilterDef (CategoryFilterRules şeması) çalıştırılır, eşleşenler channel_products'a
/// InScope=true/ScopeSource=filter olarak MATERYALİZE edilir; manuel eklenenler (ScopeSource=manual) ve
/// manuel hariç tutulanlar (IsExcluded) sync'te korunur. Kanal verisi Storefront'ta tutulur (urun-url-kanal-mimarisi §3.0).
/// </summary>
public class ChannelScope : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    /// <summary>all | filter | mixed</summary>
    public string FillType { get; set; } = "all";
    /// <summary>CategoryFilterRules ile uyumlu JSONB filtre tanımı (HasChannelPrice, MinStock dahil).</summary>
    public Dictionary<string, object>? FilterDef { get; set; }
    public DateTime? SyncedAt { get; set; }
    /// <summary>Son sync'te filtreden geçen ürün sayısı (manuel hariç).</summary>
    public int? MatchedCount { get; set; }
    public string? LastSyncError { get; set; }

    public bool IsFilterBased => FillType is "filter" or "mixed";
}
