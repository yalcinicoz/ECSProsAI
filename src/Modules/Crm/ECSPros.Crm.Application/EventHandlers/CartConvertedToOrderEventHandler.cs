using ECSPros.Crm.Application.Services;
using ECSPros.Order.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECSPros.Crm.Application.EventHandlers;

/// <summary>
/// A4 (2026-09-07): sipariş oluşan/ödemesi alınan sepetin kalemlerini siler — istemci (web/mobil)
/// artık kalem kalem DELETE atmak zorunda değil. Sepet satırı korunur (aynı üye/oturum sepeti
/// yeniden kullanılır). Hata sipariş akışını etkilemez.
/// </summary>
public class CartConvertedToOrderEventHandler(ICrmDbContext db, ILogger<CartConvertedToOrderEventHandler> logger)
    : INotificationHandler<CartConvertedToOrderEvent>
{
    public async Task Handle(CartConvertedToOrderEvent notification, CancellationToken ct)
    {
        try
        {
            var silinen = await db.CartItems
                .Where(i => i.CartId == notification.CartId)
                .ExecuteDeleteAsync(ct);
            if (silinen > 0)
                logger.LogInformation("Sepet {CartId} siparişe dönüştü (Order {OrderId}): {Adet} kalem temizlendi.",
                    notification.CartId, notification.OrderId, silinen);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sepet {CartId} sipariş sonrası temizlenemedi (Order {OrderId}).",
                notification.CartId, notification.OrderId);
        }
    }
}
