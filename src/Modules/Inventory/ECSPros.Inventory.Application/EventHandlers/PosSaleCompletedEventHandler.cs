using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Pos.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.EventHandlers;

public class PosSaleCompletedEventHandler : INotificationHandler<PosSaleCompletedEvent>
{
    private readonly IInventoryDbContext _context;

    public PosSaleCompletedEventHandler(IInventoryDbContext context)
    {
        _context = context;
    }

    public async Task Handle(PosSaleCompletedEvent notification, CancellationToken cancellationToken)
    {
        foreach (var soldItem in notification.Items)
        {
            var quantity = (int)Math.Ceiling(soldItem.Quantity);

            var stock = await _context.Stocks.FirstOrDefaultAsync(
                s => s.VariantId == soldItem.VariantId && s.WarehouseId == notification.WarehouseId,
                cancellationToken);

            if (stock == null)
            {
                stock = new Stock
                {
                    VariantId = soldItem.VariantId,
                    WarehouseId = notification.WarehouseId,
                    Quantity = 0,
                    ReservedQuantity = 0
                };
                _context.Stocks.Add(stock);
            }

            // Stok negatife düşse de kaydı kabul et — POS'ta anlık satış yapılır,
            // stok uyumsuzluğu operasyonel raporla düzeltilebilir.
            stock.Quantity = Math.Max(0, stock.Quantity - quantity);

            var movement = new StockMovement
            {
                VariantId = soldItem.VariantId,
                FromWarehouseId = notification.WarehouseId,
                ToWarehouseId = null,
                MovementType = "pos_sale",
                Quantity = quantity,
                Notes = $"POS satışı — {notification.SaleId}",
                CreatedBy = notification.CompletedBy
            };

            _context.StockMovements.Add(movement);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
