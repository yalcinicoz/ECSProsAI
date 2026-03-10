using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Order.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.EventHandlers;

/// <summary>
/// Picking plan tamamlandığında ilgili siparişlerin PackingStationCode'unu
/// boş bırakır (paketleme istasyonu atandığında ayrıca set edilecek).
/// Şu an yalnızca loglama / izleme amacıyla kayıt alır.
/// </summary>
public class PickingPlanCompletedEventHandler : INotificationHandler<PickingPlanCompletedEvent>
{
    private readonly IOrderDbContext _context;

    public PickingPlanCompletedEventHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task Handle(PickingPlanCompletedEvent notification, CancellationToken cancellationToken)
    {
        // Picking tamamlandı — siparişler paketleme kuyruğuna girdi.
        // İleride: paketleme istasyonu atama mantığı buraya veya ayrı bir event'e eklenebilir.
        var orders = await _context.Orders
            .Where(o => notification.OrderIds.Contains(o.Id) && o.Status == "processing")
            .ToListAsync(cancellationToken);

        foreach (var order in orders)
        {
            order.InternalNotes = string.IsNullOrEmpty(order.InternalNotes)
                ? $"[Toplama tamamlandı] Plan: {notification.PlanId}"
                : $"{order.InternalNotes}\n[Toplama tamamlandı] Plan: {notification.PlanId}";
        }

        if (orders.Any())
            await _context.SaveChangesAsync(cancellationToken);
    }
}
