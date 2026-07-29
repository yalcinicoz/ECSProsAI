using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

/// <summary>
/// Ürün düzeyi kategori istisnası (§2.2, K4): genel grup eşlemesine DOKUNMADAN tek ürünü
/// farklı pazaryeri kategorisine yönlendirir. Gönderimde öncelik: istisna > kural > birebir.
/// Havuz kipindeki ürün-bazlı atama da bu tabloya yazılır (Source=pool_assignment).
/// </summary>
public class MarketplaceProductCategoryOverride : BaseEntity
{
    public Guid ProductId { get; set; }
    public string Marketplace { get; set; } = string.Empty;
    public Guid? FirmPlatformId { get; set; }                 // null = firma geneli (v1 hep null)

    public string CategoryExternalId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;  // snapshot
    public string CategoryPath { get; set; } = string.Empty;  // snapshot

    /// <summary>manual: personel kararı · rejection: yükleme reddinden (Trendyol katalog
    /// çakışması, F4) · pool_assignment: havuz ataması · remote: listing senkronunda
    /// pazaryerinden okunan fiili kategori (F5).</summary>
    public string Source { get; set; } = "manual";
    public string? Note { get; set; }
}
