using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.AddOrderPayment;

public record AddOrderPaymentCommand(
    Guid OrderId,
    Guid PaymentMethodId,
    decimal Amount,
    string CurrencyCode
) : IRequest<Result<Guid>>;

public class AddOrderPaymentCommandHandler : IRequestHandler<AddOrderPaymentCommand, Result<Guid>>
{
    private readonly IOrderDbContext _db;

    public AddOrderPaymentCommandHandler(IOrderDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(AddOrderPaymentCommand request, CancellationToken ct)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order is null)
            return Result.Failure<Guid>("Sipariş bulunamadı.");

        if (order.Status == "cancelled")
            return Result.Failure<Guid>("İptal edilmiş siparişe ödeme eklenemez.");

        var payment = new OrderPayment
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            PaymentMethodId = request.PaymentMethodId,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            Status = "completed",
            CreatedAt = DateTime.UtcNow
        };

        _db.OrderPayments.Add(payment);

        // İade planı §2.6: PaymentStatus türetilmiş özet — tamamlanmış ödemeler toplamı ≥ GrandTotal → paid, aksi partial.
        var mevcutToplam = await Tahsilat.TamamlananToplamAsync(_db, order.Id, ct);
        order.PaymentStatus = Tahsilat.PaymentStatusFor(mevcutToplam + payment.Amount, order.GrandTotal);
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Result.Success(payment.Id);
    }
}
