using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECSPros.Order.Application.Commands.MockPayment;

/// <summary>
/// Demo/mock ödeme: gerçek ödeme aracısı çağrılmadan siparişi "paid" işaretler ve onaylar.
/// Yalnız PayTR yapılandırılmamış demo ortamında kullanılır (PaymentController mock dalı).
/// İdempotent: zaten paid/underpaid olan siparişe dokunmaz. CustomerNotes.payment.provider=mock.
/// </summary>
public record MockPaymentUygulaCommand(Guid OrderId, bool Basarili = true) : IRequest<Result<Guid>>;

public class MockPaymentUygulaCommandHandler(
    IOrderDbContext db,
    IPublisher publisher,
    ILogger<MockPaymentUygulaCommandHandler> logger)
    : IRequestHandler<MockPaymentUygulaCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(MockPaymentUygulaCommand request, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order is null) return Result.Failure<Guid>("Sipariş bulunamadı.");

        // İdempotent: ödeme zaten alınmışsa tekrar işleme.
        if (order.PaymentStatus is "paid" or "underpaid") return Result.Success(order.Id);

        if (!request.Basarili)
        {
            // Başarısız senaryo: sipariş failed işaretlenir, onaylanmaz (sepet korunur, tekrar denenebilir).
            var notlar = order.CustomerNotes is null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>(order.CustomerNotes);
            notlar["payment"] = new Dictionary<string, object?>
            {
                ["provider"] = "mock",
                ["status"] = "failed",
                ["failReason"] = "test_simulation",
            };
            order.CustomerNotes = notlar;
            order.PaymentStatus = "failed";
            await db.SaveChangesAsync(ct);
            return Result.Success(order.Id);
        }

        var mevcut = order.CustomerNotes is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(order.CustomerNotes);
        mevcut["payment"] = new Dictionary<string, object?>
        {
            ["provider"] = "mock",
            ["status"] = "success",
        };
        order.CustomerNotes = mevcut;
        order.PaymentStatus = "paid";
        await db.SaveChangesAsync(ct);

        // Ödeme alındı → otomatik onay (pending → confirmed). Onay, OrderConfirmedEvent ile
        // online stok rezervasyonunu tetikler (WarehouseId=Guid.Empty → depo-bağımsız).
        // Onay hatası ödeme kaydını bloklamaz; log'a düşer, personel elle onaylayabilir.
        try
        {
            order.Confirm(Guid.Empty, Guid.Empty);
            await db.SaveChangesAsync(ct);
            foreach (var domainEvent in order.DomainEvents)
                await publisher.Publish(domainEvent, ct);
            order.ClearDomainEvents();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Mock ödeme: ödeme alındı (paid) ama sipariş otomatik onaylanamadı (OrderId={OrderId}).",
                request.OrderId);
        }

        return Result.Success(order.Id);
    }
}
