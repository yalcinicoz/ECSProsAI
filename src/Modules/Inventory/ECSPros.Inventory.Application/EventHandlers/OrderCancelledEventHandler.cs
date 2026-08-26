using ECSPros.Inventory.Application.Services;
using ECSPros.Order.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.EventHandlers;

public class OrderCancelledEventHandler : INotificationHandler<OrderCancelledEvent>
{
    private readonly IInventoryDbContext _context;

    public OrderCancelledEventHandler(IInventoryDbContext context)
    {
        _context = context;
    }

    public async Task Handle(OrderCancelledEvent notification, CancellationToken cancellationToken)
    {
        // Faz 0 (StockTx): kilitlemek için varyantlar ön-sorguyla belirlenir; gövde kilit altında TAZE okur.
        var variantIds = await _context.StockReservations.AsNoTracking()
            .Where(r => r.ReferenceType == "order" && r.ReferenceId == notification.OrderId && r.Status == "reserved")
            .Select(r => r.VariantId).Distinct().ToListAsync(cancellationToken);
        if (variantIds.Count == 0) return;

        await StockTx.RunAsync(_context, variantIds, async () =>
        {
        var reservations = await _context.StockReservations
            .Where(r => r.ReferenceType == "order"
                     && r.ReferenceId == notification.OrderId
                     && r.Status == "reserved")
            .ToListAsync(cancellationToken);

        foreach (var reservation in reservations)
        {
            var stock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.Id == reservation.StockId, cancellationToken);

            if (stock != null)
                stock.ReservedQuantity = Math.Max(0, stock.ReservedQuantity - reservation.Quantity);
                // K-14 (2026-08-09): 0'lı kayıt bırakılmaz (miktar da 0'sa satır silinir)
                if (stock.Quantity == 0 && stock.ReservedQuantity == 0)
                    _context.Stocks.Remove(stock);

            reservation.Status = "cancelled";
        }

        await _context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }
}
