using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// E11: üyenin kayıtlı araması (Favori Aramalarım). Query arama metnidir
/// (/urunler?search=... ile çalıştırılır); Filters sorgu dışı filtre parametreleri
/// için ayrılmıştır (plan şartı — liste sayfası filtre entegrasyonu ileri iş,
/// bugünkü UI yalnız metin kaydeder). Bildirim gönderimi Faz H'de (StockAlert gibi);
/// NotifyEnabled o güne dek yalnız kartta "Bildirim Açık" rozetini belirler.
/// </summary>
public class SavedSearch : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid MemberId { get; set; }
    /// <summary>Görünen ad; boşsa kartta Query gösterilir.</summary>
    public string? Name { get; set; }
    public string Query { get; set; } = string.Empty;
    public Dictionary<string, string>? Filters { get; set; }
    public bool NotifyEnabled { get; set; }
}
