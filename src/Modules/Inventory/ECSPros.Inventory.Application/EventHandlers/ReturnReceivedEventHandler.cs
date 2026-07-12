using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Inventory.Domain.Events;
using ECSPros.Order.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.EventHandlers;

public class ReturnReceivedEventHandler : INotificationHandler<ReturnReceivedEvent>
{
    private readonly IInventoryDbContext _context;
    private readonly IPublisher _publisher;

    public ReturnReceivedEventHandler(IInventoryDbContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task Handle(ReturnReceivedEvent notification, CancellationToken cancellationToken)
    {
        foreach (var item in notification.Items)
        {
            var stock = await _context.Stocks.FirstOrDefaultAsync(
                s => s.VariantId == item.VariantId && s.WarehouseId == notification.WarehouseId,
                cancellationToken);

            if (stock is null)
            {
                stock = new Stock
                {
                    VariantId = item.VariantId,
                    WarehouseId = notification.WarehouseId,
                    Quantity = item.Quantity,
                    ReservedQuantity = 0
                };
                _context.Stocks.Add(stock);
            }
            else
            {
                stock.Quantity += item.Quantity;
            }

            _context.StockMovements.Add(new StockMovement
            {
                VariantId = item.VariantId,
                FromWarehouseId = null,
                ToWarehouseId = notification.WarehouseId,
                MovementType = "return",
                Quantity = item.Quantity,
                Notes = $"Sipariş iadesi — {notification.OrderId}",
                CreatedBy = notification.ReceivedBy
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        // H8: iade stok girişi de "stok gelince haber ver" kayıtlarını tetikler.
        if (notification.Items.Count > 0)
            await _publisher.Publish(
                new StockIncreasedEvent(notification.Items.Select(i => i.VariantId).Distinct().ToList()),
                cancellationToken);
    }
}
