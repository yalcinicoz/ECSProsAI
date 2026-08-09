using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

/// <summary>
/// OP1 (2026-08-09, K-13): firma bazlı operasyon profili — süreç tercihleri ve eşikler.
/// Kayıt yoksa varsayılanlar geçerli (tüm okurlar null-toleranslı davranır).
/// </summary>
public class OperationProfile : BaseEntity
{
    public Guid FirmId { get; set; }

    /// <summary>Ara ayrıştırma aşaması kullanılır mı (false = toplama → doğrudan masa).</summary>
    public bool UseIntermediateSorting { get; set; } = true;

    /// <summary>Tek ürünlü siparişler ayrı hızlı hatta toplanır.</summary>
    public bool SingleItemFastLane { get; set; } = true;

    /// <summary>Ara ayrıştırma kolisi başına maks sipariş (örn. 26).</summary>
    public int MaxOrdersPerBox { get; set; } = 26;

    /// <summary>Masa son-ayrıştırma raf (slot) sayısı.</summary>
    public int StationSlotCount { get; set; } = 26;

    /// <summary>Koli kartı renk eşikleri: "tüm ürünleri kolide" sipariş oranı yüzdesi.</summary>
    public int BoxGreenPct { get; set; } = 100;
    public int BoxYellowPct { get; set; } = 70;

    /// <summary>K-12: siparişin toplanma oranı bu eşiğin altındaysa koli seçiminde son bölgeye atılır.</summary>
    public int LowChanceThresholdPct { get; set; } = 20;

    /// <summary>Toptan: barkod + adet girişi (her ürünü tek tek okutma yerine).</summary>
    public bool BulkQuantityEntry { get; set; }

    /// <summary>K-10: kargo API bildirimi zamanı — packed | order_created.</summary>
    public string CargoNotifyAt { get; set; } = "packed";
}
