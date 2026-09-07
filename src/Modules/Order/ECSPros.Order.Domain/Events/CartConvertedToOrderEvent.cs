using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Events;

/// <summary>
/// A4 (2026-09-07, mobil): sepet siparişe dönüştü — sepetin kalemleri sunucu tarafında temizlenir.
/// Kapıda ödemede checkout anında, kartta ödeme onayında (PayTR callback / mock) yayınlanır.
/// Tüketici: Crm.CartConvertedToOrderEventHandler (hata-güvenli; siparişi etkilemez).
/// </summary>
public class CartConvertedToOrderEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid OrderId { get; }
    public Guid CartId { get; }

    public CartConvertedToOrderEvent(Guid orderId, Guid cartId)
    {
        OrderId = orderId;
        CartId = cartId;
    }
}
