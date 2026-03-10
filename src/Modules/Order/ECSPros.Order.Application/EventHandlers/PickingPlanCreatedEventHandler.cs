using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Order.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.EventHandlers;

/// <summary>
/// Picking plan oluşturulduğunda ilgili siparişleri "processing" statüsüne alır
/// ve PickingPlanId / SortingBinId alanlarını günceller.
/// </summary>
public class PickingPlanCreatedEventHandler : INotificationHandler<PickingPlanCreatedEvent>
{
    private readonly IOrderDbContext _context;

    public PickingPlanCreatedEventHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task Handle(PickingPlanCreatedEvent notification, CancellationToken cancellationToken)
    {
        var orderIds = notification.Orders.Select(o => o.OrderId).ToList();

        var orders = await _context.Orders
            .Include(o => o.Items)
            .Where(o => orderIds.Contains(o.Id))
            .ToListAsync(cancellationToken);

        foreach (var assignedOrder in notification.Orders)
        {
            var order = orders.FirstOrDefault(o => o.Id == assignedOrder.OrderId);
            if (order is null) continue;

            try
            {
                order.StartProcessing(notification.CreatedBy, notification.PlanId);
                order.PackingSlotNumber = assignedOrder.BinNumber;
            }
            catch (InvalidOperationException)
            {
                // Sipariş zaten processing'deyse atla
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
