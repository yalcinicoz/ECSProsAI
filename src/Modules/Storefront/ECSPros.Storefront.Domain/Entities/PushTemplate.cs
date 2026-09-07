using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Mobil push bildirim şablonu (docs/PUSH_BILDIRIM_ENTEGRASYONU.md §4/§7, 2026-09-07). Panelden yönetilir.
/// Type = senaryo kodu (order_shipped …); Class transactional | marketing; yer tutucular {orderNumber} {productName} …
/// LinkTemplate §3 link kataloğuna göre doğrulanır; Enabled=false → gönderilmez.
/// </summary>
public class PushTemplate : BaseEntity
{
    public string Type { get; set; } = string.Empty;
    public string Class { get; set; } = "transactional";
    public string Name { get; set; } = string.Empty;          // panel etiketi
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string LinkTemplate { get; set; } = "/";
    public bool Enabled { get; set; } = true;
    public int TtlSeconds { get; set; } = 86400;
    public string Priority { get; set; } = "high";            // high | normal
    public string? Description { get; set; }                  // tetikleyici açıklaması (panelde bilgi)
}
