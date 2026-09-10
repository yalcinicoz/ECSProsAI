using ECSPros.Inventory.Application.Services;
using ECSPros.Order.Domain.Events;
using MediatR;

namespace ECSPros.Inventory.Application.EventHandlers;

/// <summary>
/// İade planı §2.3/5 (2026-09-10): Teslimatsız İade.
///  • Kargoya VERİLMEMİŞ (faturalı, processing) → stok yalnız rezerveydi → rezervasyonlar serbest bırakılır
///    (iptalle aynı işlem); iade kalemleri StockAlreadyIn=true olduğundan "teslim al" adımı stok girişi yapmaz.
///  • Kargoya VERİLMİŞ → stok OrderShippedEvent ile tüketilmişti; depo girişi iadenin "teslim al" adımında
///    ReturnReceivedEvent ile olur. Burada yapılacak bir şey yok.
/// </summary>
public class OrderReturnedUndeliveredEventHandler(IInventoryDbContext context)
    : INotificationHandler<OrderReturnedUndeliveredEvent>
{
    public Task Handle(OrderReturnedUndeliveredEvent notification, CancellationToken cancellationToken)
        => notification.WasShipped
            ? Task.CompletedTask
            : OrderCancelledEventHandler.SiparisRezervasyonlariniSerbestBirakAsync(context, notification.OrderId, cancellationToken);
}
