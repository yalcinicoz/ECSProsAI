using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Inventory.Domain.Events;

/// <summary>
/// H8: Stok ARTIŞI gerçekleşti — "stok gelince haber ver" kayıtlarının (storefront
/// stock_alerts, C9) tüketicisi dinler. Yayınlayanlar: AdjustStock (pozitif delta),
/// ReturnReceived ve PosSaleRefunded handler'ları — stok kaydı SaveChanges'tan SONRA.
/// Miktar taşınmaz: tüketici için "bu varyantlara stok girdi" bilgisi yeterli
/// (satılabilirlik anahtarı B12'de ayrıca kapalı olabilir — bildirim yine de doğru:
/// alert zaten müşterinin gördüğü 'tükendi' durumuna karşılık açılmıştı).
/// </summary>
public class StockIncreasedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public IReadOnlyList<Guid> VariantIds { get; }

    public StockIncreasedEvent(IReadOnlyList<Guid> variantIds) => VariantIds = variantIds;
}
