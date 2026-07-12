using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Inventory.Domain.Events;
using ECSPros.Pos.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.EventHandlers;

public class PosSaleRefundedEventHandler : INotificationHandler<PosSaleRefundedEvent>
{
    private readonly IInventoryDbContext _context;
    private readonly IPublisher _publisher;

    public PosSaleRefundedEventHandler(IInventoryDbContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task Handle(PosSaleRefundedEvent notification, CancellationToken cancellationToken)
    {
        foreach (var item in notification.Items)
        {
            var quantity = (int)Math.Ceiling(item.Quantity);

            var stock = await _context.Stocks.FirstOrDefaultAsync(
                s => s.VariantId == item.VariantId && s.WarehouseId == notification.WarehouseId,
                cancellationToken);

            if (stock == null)
            {
                stock = new Stock
                {
                    VariantId = item.VariantId,
                    WarehouseId = notification.WarehouseId,
                    Quantity = quantity,
                    ReservedQuantity = 0
                };
                _context.Stocks.Add(stock);
            }
            else
            {
                stock.Quantity += quantity;
            }

            var movement = new StockMovement
            {
                VariantId = item.VariantId,
                FromWarehouseId = null,
                ToWarehouseId = notification.WarehouseId,
                MovementType = "pos_refund",
                Quantity = quantity,
                Notes = $"POS iade — {notification.SaleId}",
                CreatedBy = notification.RefundedBy
            };

            _context.StockMovements.Add(movement);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // H8: POS iadesi stok girişi de "stok gelince haber ver" kayıtlarını tetikler.
        if (notification.Items.Count > 0)
            await _publisher.Publish(
                new StockIncreasedEvent(notification.Items.Select(i => i.VariantId).Distinct().ToList()),
                cancellationToken);
    }
}
