using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.MarkDelivered;

public class MarkDeliveredCommandHandler : IRequestHandler<MarkDeliveredCommand, Result<bool>>
{
    private readonly IOrderDbContext _context;
    private readonly IPublisher _publisher;
    private readonly IPaymentMethodResolver _odemeYontemi;

    public MarkDeliveredCommandHandler(IOrderDbContext context, IPublisher publisher, IPaymentMethodResolver odemeYontemi)
    {
        _context = context;
        _publisher = publisher;
        _odemeYontemi = odemeYontemi;
    }

    public async Task<Result<bool>> Handle(MarkDeliveredCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure<bool>("Sipariş bulunamadı.");

        try
        {
            order.MarkDelivered(request.UpdatedBy);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(ex.Message);
        }

        // Kargonun teslim tarihini güncelle
        var shipment = await _context.Shipments
            .Where(s => s.OrderId == request.OrderId && s.Status == "shipped")
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (shipment is not null)
        {
            shipment.Status = "delivered";
            shipment.DeliveredAt = DateTime.UtcNow;
        }

        // İade planı §2.6 / K6 (2026-09-10): kapıda ödemede TESLİM = TAHSİLAT. Tahsilat satırı burada atılır
        // (kargo entegrasyonunun "teslim edildi" olayı da bu komuttan geçer → tek nokta); PaymentStatus paid olur.
        // Kargo firması mutabakat farkı Finance'te ayrı iş. Kural (IadeOdemeKurali) bu satıra bakar (R9).
        if (ECSPros.Shared.Contracts.IadeOdemeKurali.KapidaOdeme(order.PaymentMethod))
        {
            var yontemId = await _odemeYontemi.GetIdByCodeAsync(IPaymentMethodResolver.CoreCodeFor(order.PaymentMethod), cancellationToken) ?? Guid.Empty;
            await Tahsilat.KaydetAsync(_context, order, yontemId, order.GrandTotal, Tahsilat.KaynakTeslimdeKapidaOdeme,
                request.UpdatedBy, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // P3a: OrderDeliveredEvent — satıcı hakediş satırları host'taki handler'da üretilir
        foreach (var domainEvent in order.DomainEvents)
            await _publisher.Publish(domainEvent, cancellationToken);
        order.ClearDomainEvents();

        return Result.Success(true);
    }
}
